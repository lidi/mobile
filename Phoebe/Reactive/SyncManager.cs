using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Toggl.Phoebe.Logging;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.Json;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Helpers;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Phoebe.Reactive
{
    public class SyncManager
    {
        public class QueueItem
        {
            static readonly IDictionary<string, Type> typeCache = new Dictionary<string, Type> ();

            public string TypeName { get; set; }
            public string RawData { get; set; }

            [JsonIgnore]
            public ICommonData Data
            {
                get
                {
                    Type type;
                    if (!typeCache.TryGetValue(TypeName, out type))
                    {
                        type = Assembly.GetExecutingAssembly().GetType(TypeName);
                        typeCache.Add(TypeName, type);
                    }
                    return (ICommonData)JsonConvert.DeserializeObject(RawData, type);
                }
                set
                {
                    RawData = JsonConvert.SerializeObject(value);
                }
            }

            public QueueItem()
            {
            }

            public QueueItem(ICommonData data)
            {
                Data = data;
                TypeName = data.GetType().FullName;
            }
        }

        // sinceDate for GetChanges requests shouldn't be older than two months. Server requirements.
        // Make a few days stricter to be on the safe side.
        const int GetChangesSinceDateLimit = -56;

        const string QueueId = "SYNC_OUT";
        const string DuplicatedNameMessage = "Name has already been taken";
        const string TimeEntryConstrainMessage = "This entry can't be saved";
        const string TimeEntryUnmetConstrainst = "time entry has unmet constraints";

        public static SyncManager Singleton { get; private set; }

        public static void Init()
        {
            Singleton = Singleton ?? new SyncManager();
        }

        public static void Cleanup()
        {
            Singleton = null;
        }

        readonly string Tag = typeof(SyncManager).Name;
        readonly JsonMapper mapper;
        readonly INetworkPresence networkPresence;
        readonly ITogglClient client;
        readonly Subject<Tuple<ServerRequest, AppState>> requestManager = new Subject<Tuple<ServerRequest, AppState>> ();


        SyncManager()
        {
            mapper = new JsonMapper();
            networkPresence = ServiceContainer.Resolve<INetworkPresence> ();
            client = ServiceContainer.Resolve<ITogglClient> ();

            StoreManager.Singleton
            .Observe()
            .SelectAsync(EnqueueOrSend)
            .Subscribe((_) => { },
            (ex) =>
            {
                logError(ex, "Failed to sync. Main observer bug.");
                // Should pass a generic request?
                RxChain.Send(new DataMsg.ServerResponse(new ServerRequest.GetChanges(), ex));
            });

            requestManager
            .SelectAsync(async x => await x.Item1.MatchType(
                             (ServerRequest.DownloadEntries _) => DownloadEntries(x.Item1, x.Item2),
                             (ServerRequest.GetChanges _) => GetChanges(x.Item1, x.Item2),
                             (ServerRequest.GetCurrentState _) => GetChanges(x.Item1, x.Item2),
                             (ServerRequest.Authenticate req) =>
            {
                switch (req.Operation)
                {
                    case ServerRequest.Authenticate.Op.Login:
                        return LoginAsync(req.Username, req.Password);
                    case ServerRequest.Authenticate.Op.Signup:
                        return SignupAsync(req.Username, req.Password);
                    case ServerRequest.Authenticate.Op.LoginWithGoogle:
                        return LoginWithGoogleAsync(req.AccessToken);
                    case ServerRequest.Authenticate.Op.SignupWithGoogle:
                        return SignupWithGoogleAsync(req.AccessToken);
                    default:
                        throw new Exception("Unexpected Authenticate operation");
                }
            }))
            .Subscribe((_) => { },
            (ex) =>
            {
                logError(ex, "Failed to sync. RequestManager bug.");
                // Should pass a dummy request for this cases?
                RxChain.Send(new DataMsg.ServerResponse(new ServerRequest.GetChanges(), ex));
            });
        }

        void logError(Exception ex, string msg = "Failed to sync")
        {
            var logger = ServiceContainer.Resolve<ILogger> ();
            logger.Error(Tag, ex, msg);
        }

        void logInfo(string msg, Exception exc = null)
        {
            var logger = ServiceContainer.Resolve<ILogger> ();
            if (exc == null)
            {
                logger.Info(Tag, msg);
            }
            else
            {
                logger.Info(Tag, exc, msg);
            }
        }

        void logWarning(string msg, Exception exc = null)
        {
            var logger = ServiceContainer.Resolve<ILogger> ();
            if (exc == null)
            {
                logger.Warning(Tag, msg);
            }
            else
            {
                logger.Warning(Tag, exc, msg);
            }
        }

        async Task EnqueueOrSend(DataSyncMsg<AppState> syncMsg)
        {
            var remoteObjects = new List<CommonData> ();
            var enqueuedItems = new List<QueueItem> ();
            var isConnected = networkPresence.IsNetworkPresent;
            var dataStore = ServiceContainer.Resolve<ISyncDataStore>();

            try
            {
                // Try to empty queue first
                var queueEmpty = await TryEmptyQueue(remoteObjects, syncMsg.State, isConnected, dataStore);

                // Deal with messages
                foreach (var data in syncMsg.ServerRequests.OfType<ServerRequest.CRUD>().SelectMany(x => x.Items))
                {
                    if (queueEmpty && isConnected)
                    {
                        try
                        {
                            await SendData(data, remoteObjects, syncMsg.State);
                        }
                        catch (Exception ex)
                        {
                            logError(ex);
                            Enqueue(data, enqueuedItems, dataStore);
                            queueEmpty = false;
                        }
                    }
                    else
                    {
                        Enqueue(data, enqueuedItems, dataStore);
                        queueEmpty = false;
                    }
                }

                // TODO: Try to empty queue again?
            }
            catch (Exception ex)
            {
                logError(ex, $"{nameof(SyncManager)} Queue");
            }

            // Return remote objects
            if (remoteObjects.Count > 0)
                RxChain.Send(DataMsg.ServerResponse.CRUD(remoteObjects));

            foreach (var req in syncMsg.ServerRequests.Where(x => x is ServerRequest.CRUD == false))
            requestManager.OnNext(Tuple.Create(req, syncMsg.State));

            // Mostly used for test pourposes.
            if (syncMsg.Continuation != null && !syncMsg.Continuation.LocalOnly)
                syncMsg.Continuation.Invoke(syncMsg.State, remoteObjects, enqueuedItems);
        }

        async Task<bool> TryEmptyQueue(List<CommonData> remoteObjects, AppState state, bool isConnected, ISyncDataStore dataStore)
        {
            // Clean the queue when a logout is detected
            var authToken = state.User.ApiToken;
            if (string.IsNullOrEmpty(authToken) && dataStore.GetQueueSize(QueueId) > 0)
            {
                dataStore.ResetQueue(QueueId);
                return true;
            }

            string json = null;
            if (dataStore.TryPeek(QueueId, out json))
            {
                if (isConnected)
                {
                    try
                    {
                        do
                        {
                            var queueItem = JsonConvert.DeserializeObject<QueueItem> (json);
                            await SendData(queueItem.Data, remoteObjects, state);

                            // If we sent the message successfully, remove it from the queue
                            dataStore.TryDequeue(QueueId, out json);
                        }
                        while (dataStore.TryPeek(QueueId, out json));
                        return true;
                    }
                    catch (Exception ex)
                    {
                        logError(ex);
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return true;
            }
        }

        void Enqueue(ICommonData data, List<QueueItem> enqueuedItems, ISyncDataStore dataStore)
        {
            try
            {
                var queueItem = new QueueItem(data);
                var serialized = JsonConvert.SerializeObject(queueItem);
                dataStore.TryEnqueue(QueueId, serialized);
                enqueuedItems.Add(queueItem);
            }
            catch (Exception ex)
            {
                // TODO: Retry?
                logError(ex, "Failed to queue message");
            }
        }

        async Task SendData(ICommonData data, List<CommonData> remoteObjects, AppState state)
        {
            try
            {
                var authToken = state.User.ApiToken;
                if (data.DeletedAt == null)
                {
                    var json = PrepareForSync(data, remoteObjects, state);
                    CommonJson response = null;
                    switch (data.SyncState)
                    {
                        case SyncState.CreatePending:
                            Console.WriteLine("Remote created :" + data.GetType() + " " + data.Id);
                            response = await client.Create(authToken, json);
                            break;
                        case SyncState.UpdatePending:
                            Console.WriteLine("Remote update :" + data.GetType() + " " + data.Id);
                            response = await client.Update(authToken, json);
                            break;
                        default:
                            throw new Exception(
                                string.Format("Unexpected SyncState ({0}) of enqueued item: {1}",
                                              Enum.GetName(typeof(SyncState), data.SyncState), data.Id));
                    }
                    var resData = mapper.Map(response);
                    resData.Id = data.Id;
                    remoteObjects.Add(resData);
                    Console.WriteLine("Remote received :" + resData.GetType() + " " + resData.RemoteId + " " + resData.Id);
                }
                else
                {
                    var json = mapper.MapToJson(data);
                    // If RemoteId is null, check whether it can be found in previously sent objects and ignore if not
                    json.RemoteId = json.RemoteId ?? remoteObjects.FirstOrDefault(x => x.Id == data.Id)?.RemoteId;
                    if (json.RemoteId != null)
                    {
                        await client.Delete(authToken, json);
                        // Check if remoteObjects contains the deleted item
                        // (for example, when an entry is create and deleted before syncing)
                        for (var i = remoteObjects.Count - 1; i >= 0; i--)
                            if (remoteObjects[i].Id == data.Id)
                                remoteObjects.RemoveAt(i);
                    }
                }
            }
            catch (Exception ex)
            {
                // TODO RX: Check the rejection reason: if an item is being specifically rejected,
                // discard it so it doesn't block the syncing of other items
                // In V9, the ERROR message will arrive properly normalized.
                if (ex is UnsuccessfulRequestException)
                {
                    var exception = (UnsuccessfulRequestException)ex;
                    if (exception.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        if (exception.Message.Contains(DuplicatedNameMessage))
                        {
                            // ATTENTION Due the lack of a proper way to get data
                            // when a name conflicts occurs, we download new json only
                            // and update the object properly.
                            var response = await GetRemoteJsonForDuplicatedName(data, state);
                            var resData = mapper.Map(response);
                            resData.Id = data.Id;
                            remoteObjects.Add(resData);
                            logInfo("Requested extra info because duplicated: " + resData.GetType());
                            return;
                        }

                        if (exception.Message.Contains(TimeEntryConstrainMessage) ||
                                exception.Message.Contains(TimeEntryUnmetConstrainst))
                        {
                            // ATTENTION For errors related with the time entry restrictions
                            // show the message and remove it from queue.
                            var errorMsg = GetReadableErrorMessage(exception.Message, data);
                            var exc = new DataMsg.ServerResponse.TimeConstrainsException(errorMsg, data.Id);
                            RxChain.Send(new DataMsg.ServerResponse(new ServerRequest.CRUD(remoteObjects), exc));
                            return;
                        }
                    }

                    if (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // ATTENTION If objects are not present in server and at the same time
                        // are present in local, for data sanity is better to pass them
                        // as deleted to local state.
                        remoteObjects.Add((CommonData)UpdateToDeletePending(data));
                        logInfo("Object not found: " + data.GetType() +  " remoteId:" + data.RemoteId);
                        return;
                    }

                    if (exception.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                        return;
                }

                throw;
            }
        }

        #region Auth methods

        async Task LoginAsync(string username, string password)
        {
            logInfo(string.Format("Authenticating with email ({0}).", username));
            await AuthenticateAsync(() => client.GetUser(username, password), AuthChangeReason.Login);
        }

        async Task LoginWithGoogleAsync(string accessToken)
        {
            logInfo("Authenticating with Google access token.");
            await AuthenticateAsync(() => client.GetUser(accessToken), AuthChangeReason.LoginGoogle);
        }

        async Task SignupAsync(string email, string password)
        {
            logInfo(string.Format("Signing up with email ({0}).", email));
            await AuthenticateAsync(() => client.Create(string.Empty, new UserJson
            {
                Email = email,
                Password = password,
                Timezone = Time.TimeZoneId,
            }), AuthChangeReason.Signup); //, AccountCredentials.Password);
        }

        async Task SignupWithGoogleAsync(string accessToken)
        {
            logInfo("Signing up with email Google access token.");
            await AuthenticateAsync(() => client.Create(string.Empty, new UserJson()
            {
                GoogleAccessToken = accessToken,
                Timezone = Time.TimeZoneId,
            }), AuthChangeReason.SignupGoogle); //, AccountCredentials.Google
        }

        async Task AuthenticateAsync(
            Func<Task<UserJson>> getUser, AuthChangeReason reason) //, AccountCredentials credentialsType)
        {
            UserJson userJson = null;
            var authResult = AuthResult.Success;
            try
            {
                userJson = await getUser();
                if (userJson == null)
                {
                    authResult = (reason == AuthChangeReason.LoginGoogle) ? AuthResult.NoGoogleAccount : AuthResult.InvalidCredentials;
                }
                else if (userJson.DefaultWorkspaceRemoteId == 0)
                {
                    authResult = AuthResult.NoDefaultWorkspace;
                }
            }
            catch (Exception ex)
            {
                var reqEx = ex as UnsuccessfulRequestException;
                if (reqEx != null && (reqEx.IsForbidden || reqEx.IsValidationError))
                {
                    authResult = AuthResult.InvalidCredentials;
                }
                else
                {
                    if (ex.IsNetworkFailure() || ex is TaskCanceledException)
                    {
                        logInfo("Failed authenticate user. Network error.", ex);
                        authResult = AuthResult.NetworkError;
                    }
                    else
                    {
                        logWarning("Failed to authenticate user. Unknown error.", ex);
                        authResult = AuthResult.SystemError;
                    }
                }
            }

            // TODO RX: Ping analytics service
            //var tracker = ServiceContainer.Resolve<ITracker> ();
            //switch (reason) {
            //    case AuthChangeReason.Login:
            //        tracker.SendAccountLoginEvent (credentialsType);
            //    break;
            //    case AuthChangeReason.Signup:
            //        tracker.SendAccountCreateEvent (credentialsType);
            //    break;
            //}

            // TODO RX: Send DataMsg.ServerResponse instead
            RxChain.Send(new DataMsg.UserDataPut(
                             authResult, userJson != null ? mapper.Map<UserData> (userJson) : null));
        }

        #endregion

        #region Download methods

        async Task GetChanges(ServerRequest request, AppState state)
        {
            string authToken = state.User.ApiToken;
            DateTime? sinceDate;

            if (request is ServerRequest.GetChanges)
            {
                sinceDate = state.RequestInfo.GetChangesLastRun;
                if (sinceDate < DateTime.Now.Date.AddDays(GetChangesSinceDateLimit))
                    sinceDate = DateTime.Now.Date.AddDays(GetChangesSinceDateLimit);
            }
            else
            {
                // Request state of Data in server.
                // passing null value.
                sinceDate = null;
            }

            try
            {
                var changes = await client.GetChanges(authToken, sinceDate);

                // ATTENTION: Order is important
                var data = changes
                           .Workspaces
                           .Select(mapper.Map<WorkspaceData>).Cast<CommonData> ()
                           .Concat(changes.Tags.Select(mapper.Map<TagData>).Cast<CommonData> ())
                           .Concat(changes.Clients.Select(mapper.Map<ClientData>).Cast<CommonData> ())
                           .Concat(changes.Projects.Select(mapper.Map<ProjectData>).Cast<CommonData> ())
                           .Concat(changes.Tasks.Select(mapper.Map<TaskData>).Cast<CommonData> ())
                           .Concat(changes.TimeEntries.Select(mapper.Map<TimeEntryData>).Cast<CommonData> ())
                           .ToList();

                RxChain.Send(
                    new DataMsg.ServerResponse(
                        request, data, mapper.Map<UserData> (changes.User), changes.Timestamp));

            }
            catch (Exception exc)
            {
                string errorMsg = string.Format("Failed to sync data since {0}", state.RequestInfo.GetChangesLastRun);

                if (exc.IsNetworkFailure() || exc is TaskCanceledException)
                {
                    logInfo(errorMsg, exc);
                }
                else
                {
                    logWarning(errorMsg, exc);
                }

                RxChain.Send(new DataMsg.ServerResponse(request, exc));
            }
        }

        async Task DownloadEntries(ServerRequest request, AppState state)
        {
            long? clientRemoteId = null;
            string authToken = state.User.ApiToken;
            var startDate = state.RequestInfo.DownloadFrom;
            const int endDate = Literals.TimeEntryLoadDays;

            try
            {
                // Download new Entries
                var jsonEntries = await client.ListTimeEntries(authToken, startDate, endDate);

                var newWorkspaces = new List<WorkspaceJson> ();
                var newProjects = new List<ProjectJson> ();
                var newClients = new List<ClientJson> ();
                var newTasks = new List<TaskJson> ();
                var newTags = new List<TagData> ();

                // Check the state contains all related objects
                foreach (var entry in jsonEntries)
                {
                    if (state.Workspaces.Values.All(x => x.RemoteId != entry.WorkspaceRemoteId) &&
                            newWorkspaces.All(x => x.RemoteId != entry.WorkspaceRemoteId))
                    {
                        newWorkspaces.Add(await client.Get<WorkspaceJson> (authToken, entry.WorkspaceRemoteId));
                    }

                    if (entry.ProjectRemoteId.HasValue)
                    {
                        var projectData = state.Projects.Values.FirstOrDefault(
                                              x => x.RemoteId == entry.ProjectRemoteId);

                        if (projectData != null)
                        {
                            clientRemoteId = projectData.ClientRemoteId;
                        }
                        else
                        {
                            var projectJson = newProjects.FirstOrDefault(x => x.RemoteId == entry.ProjectRemoteId);
                            if (projectJson == null)
                            {
                                projectJson = await client.Get<ProjectJson>(authToken, entry.ProjectRemoteId.Value);
                                newProjects.Add(projectJson);
                            }
                            clientRemoteId = (projectJson as ProjectJson).ClientRemoteId;
                        }

                        if (clientRemoteId.HasValue)
                        {
                            if (state.Clients.Values.All(x => x.RemoteId != clientRemoteId) &&
                                    newClients.All(x => x.RemoteId != clientRemoteId))
                            {
                                newClients.Add(await client.Get<ClientJson>(authToken, clientRemoteId.Value));
                            }
                        }
                    }

                    if (entry.TaskRemoteId.HasValue)
                    {
                        if (state.Tasks.Values.All(x => x.RemoteId != entry.TaskRemoteId) &&
                                newTasks.All(x => x.RemoteId != entry.TaskRemoteId))
                        {
                            newTasks.Add(await client.Get<TaskJson>(authToken, entry.TaskRemoteId.Value));
                        }
                    }
                }

                // ATTENTION: Order is important, containers must come first
                // E.g. projects come after client, because projects contain a reference to ClientId
                var data = newWorkspaces
                           .Select(mapper.Map<WorkspaceData>).Cast<CommonData> ()
                           .Concat(newTags.Select(mapper.Map<TagData>).Cast<CommonData> ())
                           .Concat(newClients.Select(mapper.Map<ClientData>).Cast<CommonData> ())
                           .Concat(newProjects.Select(mapper.Map<ProjectData>).Cast<CommonData> ())
                           .Concat(newTasks.Select(mapper.Map<TaskData>).Cast<CommonData> ())
                           .Concat(jsonEntries.Select(mapper.Map<TimeEntryData>).Cast<CommonData> ())
                           .ToList();


                RxChain.Send(new DataMsg.ServerResponse(request, data));

            }
            catch (Exception exc)
            {
                string errorMsg = string.Format(
                                      "Failed to fetch time entries {1} days up to {0}",
                                      startDate, endDate);

                if (exc.IsNetworkFailure() || exc is TaskCanceledException)
                {
                    logInfo(errorMsg, exc);
                }
                else
                {
                    logWarning(errorMsg, exc);
                }

                RxChain.Send(new DataMsg.ServerResponse(request, exc));
            }
        }

        #endregion

        #region Utils

        CommonJson PrepareForSync(ICommonData data, List<CommonData> remoteObjects, AppState state)
        {
            CommonJson json;
            data = BuildRemoteRelationships(data, remoteObjects, state);
            json = mapper.MapToJson(data);

            if (data.SyncState == SyncState.UpdatePending && json.RemoteId == null)
            {
                json.RemoteId = GetRemoteId(data.Id, remoteObjects, state, data.GetType());
            }
            return json;
        }

        ICommonData BuildRemoteRelationships(ICommonData data, List<CommonData> remoteObjects, AppState state)
        {
            if (data is TimeEntryData)
            {
                var te = (TimeEntryData)data.Clone();

                if (te.UserRemoteId == 0)
                {
                    te.UserRemoteId = GetRemoteId<UserData> (te.UserId, remoteObjects, state);
                }
                if (te.WorkspaceRemoteId == 0)
                {
                    te.WorkspaceRemoteId = GetRemoteId<WorkspaceData> (te.WorkspaceId, remoteObjects, state);
                }
                if (te.ProjectId != Guid.Empty && !te.ProjectRemoteId.HasValue)
                {
                    te.ProjectRemoteId = GetRemoteId<ProjectData> (te.ProjectId, remoteObjects, state);
                }
                if (te.TaskId != Guid.Empty && !te.TaskRemoteId.HasValue)
                {
                    te.TaskRemoteId = GetRemoteId<TaskData> (te.TaskId, remoteObjects, state);
                }
                return te;
            }
            if (data is ProjectData)
            {
                var pr = (ProjectData)data.Clone();
                if (pr.WorkspaceRemoteId == 0)
                {
                    pr.WorkspaceRemoteId = GetRemoteId<ProjectData> (pr.WorkspaceId, remoteObjects, state);
                }
                if (pr.ClientId != Guid.Empty && !pr.ClientRemoteId.HasValue)
                {
                    pr.ClientRemoteId = GetRemoteId<ClientData>(pr.ClientId, remoteObjects, state);
                }
                return pr;
            }
            if (data is ClientData)
            {
                var cl = (ClientData)data.Clone();
                if (cl.WorkspaceRemoteId == 0)
                {
                    cl.WorkspaceRemoteId = GetRemoteId<WorkspaceData> (cl.WorkspaceId, remoteObjects, state);
                }
                return cl;
            }
            if (data is TaskData)
            {
                var ts = (TaskData)data.Clone();
                if (ts.WorkspaceRemoteId == 0)
                {
                    ts.WorkspaceRemoteId = GetRemoteId<TaskData> (ts.WorkspaceId, remoteObjects, state);
                }
                if (ts.ProjectRemoteId == 0)
                {
                    ts.ProjectRemoteId = GetRemoteId<ProjectData>(ts.ProjectId, remoteObjects, state);
                }
                return ts;
            }
            if (data is TagData)
            {
                var t = (TagData)data.Clone();
                if (t.WorkspaceRemoteId == 0)
                {
                    t.WorkspaceRemoteId = GetRemoteId<TagData>(t.WorkspaceId, remoteObjects, state);
                }
                return t;
            }
            if (data is WorkspaceData)
            {
                return data;
            }
            if (data is UserData)
            {
                // TODO RX: How to get DefaultWorkspaceRemoteId?
                return data;
            }
            if (data is ProjectUserData)
            {
                var pr = (ProjectUserData)data.Clone();
                if (pr.ProjectRemoteId == 0)
                {
                    pr.ProjectRemoteId = GetRemoteId<ProjectData> (pr.ProjectId, remoteObjects, state);
                }
                if (pr.UserRemoteId == 0)
                {
                    pr.UserRemoteId = GetRemoteId<UserData> (pr.UserId, remoteObjects, state);
                }
                return pr;
            }
            if (data is WorkspaceUserData)
            {
                var ws = (WorkspaceUserData)data.Clone();
                if (ws.WorkspaceRemoteId == 0)
                {
                    ws.WorkspaceRemoteId = GetRemoteId<ProjectData>(ws.WorkspaceId, remoteObjects, state);
                }
                if (ws.UserRemoteId == 0)
                {
                    ws.UserRemoteId = GetRemoteId<UserData>(ws.UserId, remoteObjects, state);
                }
                return ws;
            }
            throw new Exception("Unrecognized data type");
        }

        long GetRemoteId<T> (Guid localId, List<CommonData> remoteObjects, AppState state)
        {
            return GetRemoteId(localId, remoteObjects, state, typeof(T));
        }

        long GetRemoteId(Guid localId, List<CommonData> remoteObjects, AppState state, Type typ)
        {
            long? res = null;
            // Check first if we already received the RemoteId in the previous messages
            var d = remoteObjects.SingleOrDefault(x => x.Id == localId);
            if (d != null)
            {
                res = d.RemoteId;
            }
            else if (typ == typeof(WorkspaceData))
            {
                res = state.Workspaces[localId].RemoteId;
            }
            else if (typ == typeof(ClientData))
            {
                res = state.Clients[localId].RemoteId;
            }
            else if (typ == typeof(ProjectData))
            {
                res = state.Projects[localId].RemoteId;
            }
            else if (typ == typeof(TaskData))
            {
                res = state.Tasks[localId].RemoteId;
            }
            else if (typ == typeof(TagData))
            {
                res = state.Tags[localId].RemoteId;
            }
            else if (typ == typeof(TimeEntryData))
            {
                res = state.TimeEntries[localId].Data.RemoteId;
            }
            else if (typ == typeof(UserData))
            {
                res = state.User.RemoteId;
            }
            else if (typ == typeof(WorkspaceUserData))
            {
                res = state.WorkspaceUsers[localId].RemoteId;
            }
            else if (typ == typeof(ProjectUserData))
            {
                res = state.ProjectUsers[localId].RemoteId;
            }

            if (!res.HasValue)
            {
                // Stop sending messages and wait for state update
                // TODO RX: Keep a cache to check if the same error is repeating many times?
                // Publish more info about this bug
                throw new Exception("RemoteId missing. Type: " + typ.FullName);
            }
            return res.Value;
        }

        private ICommonData UpdateToCreatePending(ICommonData data)
        {
            if (data is ITimeEntryData)
                return ((ITimeEntryData)data).With(x => { x.SyncState = SyncState.CreatePending; x.RemoteId = null;});

            if (data is IClientData)
                return ((IClientData)data).With(x => { x.SyncState = SyncState.CreatePending; x.RemoteId = null;});

            if (data is IProjectData)
                return ((IProjectData)data).With(x => { x.SyncState = SyncState.CreatePending; x.RemoteId = null;});

            if (data is ITagData)
                return ((ITagData)data).With(x => { x.SyncState = SyncState.CreatePending; x.RemoteId = null;});

            return data;
        }

        private ICommonData UpdateToDeletePending(ICommonData data)
        {
            if (data is ITimeEntryData)
                return ((ITimeEntryData)data).With(x => x.DeletedAt = Time.Now);

            if (data is IClientData)
                return ((IClientData)data).With(x => x.DeletedAt = Time.Now);

            if (data is IProjectData)
                return ((IProjectData)data).With(x => x.DeletedAt = Time.Now);

            if (data is ITagData)
                return ((ITagData)data).With(x => x.DeletedAt = Time.Now);

            return data;
        }

        private async Task<CommonJson> GetRemoteJsonForDuplicatedName(ICommonData data, AppState state)
        {
            var auth = state.User.ApiToken;
            var since = state.RequestInfo.GetChangesLastRun;

            if (data is ITagData)
            {
                var remoteWorkspaceId = state.Workspaces[((ITagData)data).WorkspaceId].RemoteId;
                var tags = await client.GetSince<TagJson>(auth, since);
                return tags.FirstOrDefault(x => x.Name == ((ITagData)data).Name && x.WorkspaceRemoteId == remoteWorkspaceId);
            }

            if (data is IProjectData)
            {
                var remoteWorkspaceId = state.Workspaces [((IProjectData)data).WorkspaceId].RemoteId;
                var projects = await client.GetSince<ProjectJson> (auth, since);
                return projects.FirstOrDefault(x => x.Name == ((IProjectData)data).Name && x.WorkspaceRemoteId == remoteWorkspaceId);
            }

            if (data is IClientData)
            {
                var remoteWorkspaceId = state.Workspaces [((IClientData)data).WorkspaceId].RemoteId;
                var clients = await client.GetSince<ClientJson> (auth, since);
                return clients.FirstOrDefault(x => x.Name == ((IClientData)data).Name && x.WorkspaceRemoteId == remoteWorkspaceId);
            }

            throw new Exception("Type not supported. Sync Duplicated. Type: " + data.GetType());

        }

        private string GetReadableErrorMessage(string msg, ICommonData data)
        {
            var separatorIndex = msg.IndexOf("- ", StringComparison.Ordinal);
            var reason = msg.Substring(separatorIndex + 2, msg.Length - separatorIndex - 4);
            var readableMsg = "The time entry started at: " + ((ITimeEntryData)data).StartTime.ToLocalTime().ToShortTimeString()
                              + " with duration " + ((ITimeEntryData)data).GetDuration().Truncate(TimeSpan.TicksPerSecond)
                              + " can't be saved.";
            readableMsg = readableMsg + "\n\n" + char.ToUpper(reason[0]) + reason.Substring(1) + ".";
            return readableMsg;
        }

        #endregion
    }
}
