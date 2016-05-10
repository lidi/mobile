using System;
using System.Collections.Generic;
using System.Linq;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Helpers;
using Toggl.Phoebe.Net;
using Toggl.Phoebe.Reactive;

namespace Toggl.Phoebe.Data
{
    public abstract class DataMsg
    {
        protected Either<object, Exception> RawData { get; set; }

        protected DataMsg()
        {
            RawData = Either<object, Exception>.Left(null);
        }

        public sealed class ServerRequest : DataMsg
        {
            public Data.ServerRequest Data
            {
                get { return RawData.ForceLeft() as Data.ServerRequest; }
                private set { RawData = Either<object, Exception>.Left(value); }
            }

            public ServerRequest(Data.ServerRequest req)
            {
                Data = req;
            }
        }

        public sealed class ServerResponse : DataMsg
        {
            public class TimeConstrainsException : Exception
            {
                public string ReadableMsg { get; }
                public Guid CommonDataId { get; }

                public TimeConstrainsException(string msg, Guid dataId)
                {
                    ReadableMsg = msg;
                    CommonDataId = dataId;
                }
            }

            public Data.ServerRequest Request { get; private set; }

            public UserData User { get; private set; }

            public DateTime Timestamp { get; private set; }

            public Either<IEnumerable<CommonData>, Exception> Data
            {
                get { return RawData.CastLeft<IEnumerable<CommonData>> (); }
                private set { RawData = value.CastLeft<object> (); }
            }

            public ServerResponse(Data.ServerRequest req, Exception ex)
            {
                Request = req;
                Data = Either<IEnumerable<CommonData>, Exception>.Right(ex);
            }

            public ServerResponse(
                Data.ServerRequest req, IEnumerable<CommonData> data,
                UserData user = null, DateTime? timestamp = null)
            {
                Request = req;
                User = user;
                Timestamp = timestamp.HasValue ? timestamp.Value : DateTime.MinValue;
                Data = Either<IEnumerable<CommonData>, Exception>.Left(data);
            }

            public static ServerResponse CRUD(IEnumerable<CommonData> data) =>
            new ServerResponse(new Data.ServerRequest.CRUD(new ICommonData[] { }), data);
        }

        public sealed class ResetState : DataMsg
        {
        }

        public sealed class InitStateAfterMigration : DataMsg
        {
        }

        public sealed class TimeEntriesLoad : DataMsg
        {
        }

        public sealed class TimeEntryStart : DataMsg
        {
        }

        public sealed class TimeEntryStop : DataMsg
        {
            public Either<ITimeEntryData, Exception> Data
            {
                get { return RawData.CastLeft<ITimeEntryData> (); }
                private set { RawData = value.CastLeft<object> (); }
            }

            public TimeEntryStop(ITimeEntryData data)
            {
                Data = Either<ITimeEntryData, Exception>.Left(data);
            }
        }

        public sealed class TimeEntryContinue : DataMsg
        {
            public bool StartedByFAB { get; private set; }
            public Either<ITimeEntryData, Exception> Data
            {
                get { return RawData.CastLeft<ITimeEntryData> (); }
                private set { RawData = value.CastLeft<object> (); }
            }

            public TimeEntryContinue(ITimeEntryData data, bool startedByFAB = false)
            {
                StartedByFAB = startedByFAB;
                Data = Either<ITimeEntryData, Exception>.Left(data);
            }
        }

        public sealed class TimeEntryPut : DataMsg
        {
            public IEnumerable<string> TagNames { get; }
            public Either<ITimeEntryData, Exception> Data
            {
                get { return RawData.CastLeft<ITimeEntryData> (); }
                private set { RawData = value.CastLeft<object> (); }
            }

            public TimeEntryPut(ITimeEntryData data)
            {
                Data = Either<ITimeEntryData, Exception>.Left(data);
                TagNames = data.Tags;
            }

            public TimeEntryPut(ITimeEntryData data, IEnumerable<string> tagNames)
            {
                TagNames = tagNames;
                Data = Either<ITimeEntryData, Exception>.Left(data);
            }
        }

        public sealed class TimeEntriesRemove : DataMsg
        {
            public Either<IEnumerable<ITimeEntryData>, Exception> Data
            {
                get { return RawData.CastLeft<IEnumerable<ITimeEntryData>> (); }
                private set { RawData = value.CastLeft<object> (); }
            }

            public TimeEntriesRemove(ITimeEntryData data)
            : this(new ITimeEntryData[] { data })
            {
            }

            public TimeEntriesRemove(IEnumerable<ITimeEntryData> data)
            {
                Data = Either<IEnumerable<ITimeEntryData>, Exception>.Left(data);
            }
        }

        public sealed class TagsPut : DataMsg
        {
            public Either<IEnumerable<ITagData>, Exception> Data
            {
                get { return RawData.CastLeft<IEnumerable<ITagData>> (); }
                private set { RawData = value.CastLeft<object> (); }
            }

            public TagsPut(IEnumerable<ITagData> tags)
            {
                Data = Either<IEnumerable<ITagData>, Exception>.Left(tags);
            }
        }

        // Launch this message when connection has been recovered after a while
        public sealed class EmptyQueueAndSync : DataMsg
        {
            public Either<DateTime, Exception> Data
            {
                get { return RawData.CastLeft<DateTime> (); }
                private set { RawData = value.CastLeft<object> (); }
            }

            public EmptyQueueAndSync(DateTime data)
            {
                Data = Either<DateTime, Exception>.Left(data);
            }
        }

        public sealed class ProjectDataPut : DataMsg
        {
            public Either<IProjectData, Exception> Data
            {
                get { return RawData.CastLeft<IProjectData> (); }
                private set { RawData = value.CastLeft<object> (); }
            }

            public ProjectDataPut(IProjectData project)
            {
                Data = Either<IProjectData, Exception>.Left(project);
            }
        }

        public sealed class ClientDataPut : DataMsg
        {
            public Either<IClientData, Exception> Data
            {
                get { return RawData.CastLeft<IClientData> (); }
                private set { RawData = value.CastLeft<object> (); }
            }

            public ClientDataPut(IClientData data)
            {
                Data = Either<IClientData, Exception>.Left(data);
            }
        }

        public sealed class UserDataPut : DataMsg
        {
            public class AuthException : Exception
            {
                public AuthResult AuthResult { get; private set; }
                public AuthException(AuthResult authResult)
                : base(Enum.GetName(typeof(AuthResult), authResult))
                {
                    AuthResult = authResult;
                }
            }

            public Either<IUserData, AuthException> Data
            {
                get { return RawData.Cast<IUserData, AuthException> (); }
                private set { RawData = value.Cast<object, Exception> (); }
            }

            public UserDataPut(AuthResult authResult, IUserData data = null)
            {
                if (authResult == AuthResult.Success && data != null)
                {
                    Data = Either<IUserData, AuthException>.Left(data);
                }
                else
                {
                    Data = Either<IUserData, AuthException>.Right(new AuthException(authResult));
                }
            }
        }

        public sealed class UpdateSetting : DataMsg
        {
            public class SettingChangeInfo : Tuple<string, object>
            {
                public SettingChangeInfo(string propName, object value) : base(propName, value)
                {
                }
            }

            public Either<SettingChangeInfo, Exception> Data
            {
                get { return RawData.CastLeft<SettingChangeInfo> (); }
                private set { RawData = value.CastLeft<object> (); }
            }

            public UpdateSetting(string settingName, object value)
            {
                var info = new SettingChangeInfo(settingName, value);
                Data = Either<SettingChangeInfo, Exception>.Left(info);
            }
        }

        public sealed class RegisterPush : DataMsg
        {
        }

        public sealed class UnregisterPush : DataMsg
        {
        }
    }

    public abstract class ServerRequest
    {
        protected ServerRequest() {}

        public sealed class CRUD : ServerRequest
        {
            public IReadOnlyList<ICommonData> Items { get; private set; }

            public CRUD(IEnumerable<ICommonData> items)
            {
                Items = items.ToList();
            }
        }

        public sealed class GetCurrentState : ServerRequest
        {
        }

        public sealed class GetChanges : ServerRequest
        {
        }

        public sealed class DownloadEntries : ServerRequest
        {
        }

        public sealed class Authenticate : ServerRequest
        {
            public enum Op
            {
                Login,
                Signup,
                LoginWithGoogle,
                SignupWithGoogle
            }

            public Op Operation { get; private set; }
            public string Username { get; private set; }
            public string Password { get; private set; }
            public string AccessToken { get; private set; }

            private Authenticate() : base() { }

            public static Authenticate Login(string email, string password)
            {
                return new Authenticate
                {
                    Operation = Op.Login,
                    Username = email,
                    Password = password
                };
            }

            public static Authenticate Signup(string email, string password)
            {
                return new Authenticate
                {
                    Operation = Op.Signup,
                    Username = email,
                    Password = password
                };
            }

            public static Authenticate LoginWithGoogle(string accessToken)
            {
                return new Authenticate
                {
                    Operation = Op.LoginWithGoogle,
                    AccessToken = accessToken
                };
            }

            public static Authenticate SignupWithGoogle(string accessToken)
            {
                return new Authenticate
                {
                    Operation = Op.SignupWithGoogle,
                    AccessToken = accessToken
                };
            }
        }
    }

    public class DataSyncMsg<T>
    {
        public T State { get; private set; }
        public RxChain.Continuation Continuation { get; private set; }
        public IReadOnlyList<ServerRequest> ServerRequests { get; private set; }

        public DataSyncMsg(T state, IEnumerable<ServerRequest> serverRequests, RxChain.Continuation cont = null)
        {
            DataSyncMsg.CheckSyncDataOrder(serverRequests);
            State = state;
            ServerRequests = serverRequests.ToList();
            Continuation = cont;
        }

        public DataSyncMsg<U> Cast<U> () =>
        new DataSyncMsg<U> ((U)(object)State, ServerRequests, Continuation);
    }

    public static class DataSyncMsg
    {
        public static IReadOnlyList<string> DataOrder = new string[]
        {
            nameof(UserData), nameof(WorkspaceData), nameof(ClientData), nameof(ProjectData),
            nameof(TaskData), nameof(TagData), nameof(TimeEntryData)
        };

        public static void CheckSyncDataOrder(IEnumerable<ServerRequest> serverRequests)
        {
            foreach (var req in serverRequests.OfType<ServerRequest.CRUD> ())
            {
                int lastIndex = 0;
                foreach (var data in req.Items)
                {
                    var dataType = data.GetType().Name;
                    var curIndex = DataOrder.IndexOf(t => t == dataType);
                    if (curIndex < lastIndex)
                    {
                        throw new Exception(string.Format("{0} cannot come after {1}", dataType, DataOrder[lastIndex]));
                    }
                    lastIndex = curIndex;
                }
            }
        }

        static public DataSyncMsg<T> Create<T> (T state) =>
        new DataSyncMsg<T> (state, new List<ServerRequest> ());

        static public DataSyncMsg<T> Create<T> (ServerRequest request, T state) =>
        new DataSyncMsg<T> (state, new List<ServerRequest> { request });

        static public DataSyncMsg<T> Create<T> (IEnumerable<ICommonData> syncData, T state) =>
        new DataSyncMsg<T> (state, new List<ServerRequest> { new ServerRequest.CRUD(syncData) });
    }
}
