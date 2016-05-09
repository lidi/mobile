using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Toggl.Phoebe.Data.Json;
using XPlatUtils;

namespace Toggl.Phoebe.Net
{
    public class TogglRestClient : ITogglClient
    {
        private static readonly DateTime UnixStart = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        private readonly Uri v8Url;
        private readonly Uri v9Url;

        public TogglRestClient(Uri url)
        {
            v8Url = new Uri(url, "v8/");
            v9Url = new Uri(url, "v9/");
        }

        private HttpClient MakeHttpClient()
        {
            // Cannot share HttpClient instance between threads as it might (and will) cause InvalidOperationExceptions
            // occasionally.

#if __TESTS__
            var client = new HttpClient()
#else
            var client = new HttpClient(new ModernHttpClient.NativeMessageHandler())
#endif
            {
                Timeout = TimeSpan.FromSeconds(10),
            };

            var headers = client.DefaultRequestHeaders;
            headers.UserAgent.Clear();
            headers.UserAgent.Add(new ProductInfoHeaderValue(Platform.AppIdentifier, Platform.AppVersion));
            headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return client;
        }

        public async Task<T> Create<T> (string authToken, T jsonObject)
        where T : CommonJson
        {
            var type = jsonObject.GetType();
            if (type == typeof(ClientJson))
            {
                return (T)(object)await CreateClient(authToken, (ClientJson)(object)jsonObject);
            }
            else if (type == typeof(ProjectJson))
            {
                return (T)(object)await CreateProject(authToken, (ProjectJson)(object)jsonObject);
            }
            else if (type == typeof(TaskJson))
            {
                return (T)(object)await CreateTask(authToken, (TaskJson)(object)jsonObject);
            }
            else if (type == typeof(TimeEntryJson))
            {
                return (T)(object)await CreateTimeEntry(authToken, (TimeEntryJson)(object)jsonObject);
            }
            else if (type == typeof(WorkspaceJson))
            {
                return (T)(object)await CreateWorkspace(authToken, (WorkspaceJson)(object)jsonObject);
            }
            else if (type == typeof(UserJson))
            {
                return (T)(object)await CreateUser((UserJson)(object)jsonObject);
            }
            else if (type == typeof(TagJson))
            {
                return (T)(object)await CreateTag(authToken, (TagJson)(object)jsonObject);
            }
            else if (type == typeof(WorkspaceUserJson))
            {
                return (T)(object)await CreateWorkspaceUser(authToken, (WorkspaceUserJson)(object)jsonObject);
            }
            else if (type == typeof(ProjectUserJson))
            {
                return (T)(object)await CreateProjectUser(authToken, (ProjectUserJson)(object)jsonObject);
            }
            else
            {
                throw new NotSupportedException(string.Format("Creating of {0} is not supported.", type));
            }
        }

        public async Task<T> Get<T> (string authToken, long id)
        where T : CommonJson
        {
            var type = typeof(T);
            if (type == typeof(ClientJson))
            {
                return (T)(object)await GetClient(authToken, id);
            }
            else if (type == typeof(ProjectJson))
            {
                return (T)(object)await GetProject(authToken, id);
            }
            else if (type == typeof(TaskJson))
            {
                return (T)(object)await GetTask(authToken, id);
            }
            else if (type == typeof(TimeEntryJson))
            {
                return (T)(object)await GetTimeEntry(authToken, id);
            }
            else if (type == typeof(WorkspaceJson))
            {
                return (T)(object)await GetWorkspace(authToken, id);
            }
            else if (type == typeof(UserJson))
            {
                return (T)(object)await GetUser(id);
            }
            else
            {
                throw new NotSupportedException(string.Format("Fetching of {0} is not supported.", type));
            }
        }

        public async Task<List<T>> GetSince<T> (string authToken, DateTime? since)
        where T : CommonJson
        {
            var type = typeof(T);
            if (type == typeof(TagJson))
            {
                return (List<T>)(object)await GetTagsSince(authToken, since);
            }
            else if (type == typeof(ProjectJson))
            {
                return (List<T>)(object)await GetProjectsSince(authToken, since);
            }
            else if (type == typeof(ClientJson))
            {
                return (List<T>)(object)await GetClientsSince(authToken, since);
            }
            else
            {
                throw new NotSupportedException(string.Format("Listing of {0} is not supported.", type));
            }
        }

        public async Task<List<T>> List<T> (string authToken)
        where T : CommonJson
        {
            var type = typeof(T);
            if (type == typeof(ClientJson))
            {
                return (List<T>)(object)await ListClients(authToken);
            }
            else if (type == typeof(TimeEntryJson))
            {
                return (List<T>)(object)await ListTimeEntries(authToken);
            }
            else if (type == typeof(WorkspaceJson))
            {
                return (List<T>)(object)await ListWorkspaces(authToken);
            }
            else
            {
                throw new NotSupportedException(string.Format("Listing of {0} is not supported.", type));
            }
        }

        public async Task<T> Update<T> (string authToken, T jsonObject)
        where T : CommonJson
        {
            var type = jsonObject.GetType();
            if (type == typeof(ClientJson))
            {
                return (T)(object)await UpdateClient(authToken, (ClientJson)(object)jsonObject);
            }
            else if (type == typeof(ProjectJson))
            {
                return (T)(object)await UpdateProject(authToken, (ProjectJson)(object)jsonObject);
            }
            else if (type == typeof(TaskJson))
            {
                return (T)(object)await UpdateTask(authToken, (TaskJson)(object)jsonObject);
            }
            else if (type == typeof(TimeEntryJson))
            {
                return (T)(object)await UpdateTimeEntry(authToken, (TimeEntryJson)(object)jsonObject);
            }
            else if (type == typeof(WorkspaceJson))
            {
                return (T)(object)await UpdateWorkspace(authToken, (WorkspaceJson)(object)jsonObject);
            }
            else if (type == typeof(UserJson))
            {
                return (T)(object)await UpdateUser(authToken, (UserJson)(object)jsonObject);
            }
            else if (type == typeof(TagJson))
            {
                return (T)(object)await UpdateTag(authToken, (TagJson)(object)jsonObject);
            }
            else if (type == typeof(WorkspaceUserJson))
            {
                return (T)(object)await UpdateWorkspaceUser(authToken, (WorkspaceUserJson)(object)jsonObject);
            }
            else if (type == typeof(ProjectUserJson))
            {
                return (T)(object)await UpdateProjectUser(authToken, (ProjectUserJson)(object)jsonObject);
            }
            else
            {
                throw new NotSupportedException(string.Format("Updating of {0} is not supported.", type));
            }
        }

        public async Task Delete<T> (string authToken, T jsonObject)
        where T : CommonJson
        {
            var type = jsonObject.GetType();
            if (type == typeof(ClientJson))
            {
                await DeleteClient(authToken, (ClientJson)(object)jsonObject);
            }
            else if (type == typeof(ProjectJson))
            {
                await DeleteProject(authToken, (ProjectJson)(object)jsonObject);
            }
            else if (type == typeof(TaskJson))
            {
                await DeleteTask(authToken, (TaskJson)(object)jsonObject);
            }
            else if (type == typeof(TimeEntryJson))
            {
                await DeleteTimeEntry(authToken, (TimeEntryJson)(object)jsonObject);
            }
            else if (type == typeof(TagJson))
            {
                await DeleteTag(authToken, (TagJson)(object)jsonObject);
            }
            else if (type == typeof(WorkspaceUserJson))
            {
                await DeleteWorkspaceUser(authToken, (WorkspaceUserJson)(object)jsonObject);
            }
            else if (type == typeof(ProjectUserJson))
            {
                await DeleteProjectUser(authToken, (ProjectUserJson)(object)jsonObject);
            }
            else if (type == typeof(UserJson))
            {
                await DeleteUser(authToken);
            }
            else
            {
                throw new NotSupportedException(string.Format("Deleting of {0} is not supported.", type));
            }
        }

        public async Task Delete<T> (string authToken, IEnumerable<T> jsonObjects)
        where T : CommonJson
        {
            var type = typeof(T);
            if (type == typeof(ClientJson))
            {
                await Task.WhenAll(jsonObjects.Select((object json) => DeleteClient(authToken, (ClientJson)json)));
            }
            else if (type == typeof(ProjectJson))
            {
                await DeleteProjects(authToken, jsonObjects as IEnumerable<ProjectJson>);
            }
            else if (type == typeof(TaskJson))
            {
                await DeleteTasks(authToken, jsonObjects as IEnumerable<TaskJson>);
            }
            else if (type == typeof(TimeEntryJson))
            {
                await Task.WhenAll(jsonObjects.Select((object json) => DeleteTimeEntry(authToken, (TimeEntryJson)json)));
            }
            else if (type == typeof(CommonJson))
            {
                // Cannot use LINQ due to AOT failure when using lambdas that use generic method calls inside them.
                var tasks = new List<Task> ();
                foreach (var json in jsonObjects)
                {
                    tasks.Add(Delete(authToken, json));
                }
                await Task.WhenAll(tasks);
            }
            else
            {
                throw new NotSupportedException(string.Format("Batch deleting of {0} is not supported.", type));
            }
        }

        private string StringifyJson(CommonJson jsonObject)
        {
            var type = jsonObject.GetType();

            string dataKey;
            if (type == typeof(TimeEntryJson))
            {
                dataKey = "time_entry";
            }
            else if (type == typeof(ProjectJson))
            {
                dataKey = "project";
            }
            else if (type == typeof(ClientJson))
            {
                dataKey = "client";
            }
            else if (type == typeof(TaskJson))
            {
                dataKey = "task";
            }
            else if (type == typeof(WorkspaceJson))
            {
                dataKey = "workspace";
            }
            else if (type == typeof(UserJson))
            {
                dataKey = "user";
            }
            else if (type == typeof(TagJson))
            {
                dataKey = "tag";
            }
            else if (type == typeof(WorkspaceUserJson))
            {
                dataKey = "workspace_user";
            }
            else if (type == typeof(ProjectUserJson))
            {
                dataKey = "project_user";
            }
            else
            {
                throw new ArgumentException(string.Format("Don't know how to handle JSON object of type {0}.", type), "jsonObject");
            }

            var json = new JObject();
            json.Add(dataKey, JObject.FromObject(jsonObject));
            return json.ToString(Formatting.None);
        }

        private HttpRequestMessage SetupRequest(string authToken, HttpRequestMessage req)
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.ASCII.GetBytes(
                                               string.Format("{0}:api_token", authToken))));
            return req;
        }

        private async Task PrepareResponse(HttpResponseMessage resp, TimeSpan requestTime)
        {
            // TODO RX: Eliminate MessageBus
            ServiceContainer.Resolve<MessageBus> ().Send(new TogglHttpResponseMessage(this, resp, requestTime));

            if (!resp.IsSuccessStatusCode)
            {
                string content = string.Empty;
                if (resp.Content != null)
                {
                    content = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
                throw new UnsuccessfulRequestException(resp.StatusCode, resp.ReasonPhrase + ":" + content);
            }
        }

        private Task<HttpResponseMessage> SendAsync(HttpRequestMessage httpReq)
        {
            return SendAsync(httpReq, CancellationToken.None);
        }

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage httpReq, CancellationToken cancellationToken)
        {
            using(var httpClient = MakeHttpClient())
            {
                var reqTimer = Stopwatch.StartNew();
                HttpResponseMessage httpResp;
                httpResp = await httpClient.SendAsync(httpReq, cancellationToken).ConfigureAwait(false);
                reqTimer.Stop();
                await PrepareResponse(httpResp, reqTimer.Elapsed);
                return httpResp;
            }
        }

        private async Task<T> CreateObject<T> (string authToken, Uri url, T jsonObject)
        where T : CommonJson, new()
        {
            var json = StringifyJson(jsonObject);
            var httpReq = SetupRequest(authToken, new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = url,
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
            var httpResp = await SendAsync(httpReq).ConfigureAwait(false);
            var respData = await httpResp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var wrap = JsonConvert.DeserializeObject<Wrapper<T>> (respData);
            return wrap.Data;
        }

        private async Task<T> GetObject<T> (string authToken, Uri url)
        where T : CommonJson, new()
        {
            var httpReq = SetupRequest(authToken, new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = url,
            });
            var httpResp = await SendAsync(httpReq).ConfigureAwait(false);
            var respData = await httpResp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var wrap = JsonConvert.DeserializeObject<Wrapper<T>> (respData);
            return wrap.Data;
        }

        private async Task<T> UpdateObject<T> (string authToken, Uri url, T jsonObject)
        where T : CommonJson, new()
        {
            var json = StringifyJson(jsonObject);
            var httpReq = SetupRequest(authToken, new HttpRequestMessage()
            {
                Method = HttpMethod.Put,
                RequestUri = url,
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
            var httpResp = await SendAsync(httpReq).ConfigureAwait(false);
            var respData = await httpResp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var wrap = JsonConvert.DeserializeObject<Wrapper<T>> (respData);
            return wrap.Data;
        }

        private Task<List<T>> ListObjects<T> (string authToken, Uri url)
        where T : CommonJson, new()
        {
            return ListObjects<T> (authToken, url, CancellationToken.None);
        }

        private async Task<List<T>> ListObjects<T> (string authToken, Uri url, CancellationToken cancellationToken)
        where T : CommonJson, new()
        {
            var httpReq = SetupRequest(authToken, new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = url,
            });
            var httpResp = await SendAsync(httpReq, cancellationToken).ConfigureAwait(false);
            var respData = await httpResp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<List<T>> (respData) ?? new List<T> (0);
        }

        private async Task DeleteObject(string authToken, Uri url)
        {
            var httpReq = SetupRequest(authToken, new HttpRequestMessage()
            {
                Method = HttpMethod.Delete,
                RequestUri = url,
            });
            await SendAsync(httpReq).ConfigureAwait(false);
        }

        private Task DeleteObjects(string authToken, Uri url)
        {
            return DeleteObject(authToken, url);
        }

        #region Client methods

        public Task<ClientJson> CreateClient(string authToken, ClientJson jsonObject)
        {
            var url = new Uri(v8Url, "clients");
            return CreateObject(authToken, url, jsonObject);
        }

        public Task<ClientJson> GetClient(string authToken, long id)
        {
            var url = new Uri(v8Url, string.Format("clients/{0}", id.ToString()));
            return GetObject<ClientJson> (authToken, url);
        }

        public Task<List<ClientJson>> ListClients(string authToken)
        {
            var url = new Uri(v8Url, "clients");
            return ListObjects<ClientJson> (authToken, url);
        }

        public Task<List<ClientJson>> ListWorkspaceClients(string authToken, long workspaceId)
        {
            var url = new Uri(v8Url, string.Format("workspaces/{0}/clients", workspaceId.ToString()));
            return ListObjects<ClientJson> (authToken, url);
        }

        public Task<ClientJson> UpdateClient(string authToken, ClientJson jsonObject)
        {
            var url = new Uri(v8Url, string.Format("clients/{0}", jsonObject.RemoteId.Value.ToString()));
            return UpdateObject(authToken, url, jsonObject);
        }

        public Task DeleteClient(string authToken, ClientJson jsonObject)
        {
            var url = new Uri(v8Url, string.Format("clients/{0}", jsonObject.RemoteId.Value.ToString()));
            return DeleteObject(authToken, url);
        }

        public async Task<List<ClientJson>> GetClientsSince(string authToken, DateTime? since)
        {
            since = null;
            var httpReq = GetV9SinceRequest(authToken, "me/projects", since);
            var httpResp = await SendAsync(httpReq).ConfigureAwait(false);
            var respData = await httpResp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var wrap = JsonConvert.DeserializeObject<List<ClientJson>> (respData);
            return wrap;
        }

        #endregion

        #region Project methods

        public Task<ProjectJson> CreateProject(string authToken, ProjectJson jsonObject)
        {
            var url = new Uri(v8Url, "projects");
            return CreateObject(authToken, url, jsonObject);
        }

        public Task<ProjectJson> GetProject(string authToken, long id)
        {
            var url = new Uri(v8Url, string.Format("projects/{0}", id.ToString()));
            return GetObject<ProjectJson> (authToken, url);
        }

        public Task<List<ProjectJson>> ListWorkspaceProjects(string authToken, long workspaceId)
        {
            var url = new Uri(v8Url, string.Format("workspaces/{0}/projects", workspaceId.ToString()));
            return ListObjects<ProjectJson> (authToken, url);
        }

        public Task<List<WorkspaceUserJson>> ListWorkspaceUsers(string authToken, long workspaceId)
        {
            var url = new Uri(v8Url, string.Format("workspaces/{0}/workspace_users", workspaceId.ToString()));
            return ListObjects<WorkspaceUserJson> (authToken, url);
        }

        public Task<ProjectJson> UpdateProject(string authToken, ProjectJson jsonObject)
        {
            var url = new Uri(v8Url, string.Format("projects/{0}", jsonObject.RemoteId.Value.ToString()));
            return UpdateObject(authToken, url, jsonObject);
        }

        public Task DeleteProject(string authToken, ProjectJson jsonObject)
        {
            var url = new Uri(v8Url, string.Format("projects/{0}", jsonObject.RemoteId.Value.ToString()));
            return DeleteObject(authToken, url);
        }

        public Task DeleteProjects(string authToken, IEnumerable<ProjectJson> jsonObjects)
        {
            var url = new Uri(v8Url, string.Format("projects/{0}",
                                                   string.Join(",", jsonObjects.Select((model) => model.RemoteId.Value.ToString()))));
            return DeleteObjects(authToken, url);
        }

        public async Task<List<ProjectJson>> GetProjectsSince(string authToken, DateTime? since)
        {
            since = null;
            var httpReq = GetV9SinceRequest(authToken, "me/projects", since);
            var httpResp = await SendAsync(httpReq).ConfigureAwait(false);
            var respData = await httpResp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var wrap = JsonConvert.DeserializeObject<List<ProjectJson>> (respData);
            return wrap;
        }

        #endregion

        #region Task methods

        public Task<TaskJson> CreateTask(string authToken, TaskJson jsonObject)
        {
            var url = new Uri(v8Url, "tasks");
            return CreateObject(authToken, url, jsonObject);
        }

        public Task<TaskJson> GetTask(string authToken, long id)
        {
            var url = new Uri(v8Url, string.Format("tasks/{0}", id.ToString()));
            return GetObject<TaskJson> (authToken, url);
        }

        public Task<List<TaskJson>> ListProjectTasks(string authToken, long projectId)
        {
            var url = new Uri(v8Url, string.Format("projects/{0}/tasks", projectId.ToString()));
            return ListObjects<TaskJson> (authToken, url);
        }

        public Task<List<ProjectUserJson>> ListProjectUsers(string authToken, long projectId)
        {
            var url = new Uri(v8Url, string.Format("projects/{0}/project_users", projectId.ToString()));
            return ListObjects<ProjectUserJson> (authToken, url);
        }

        public Task<List<TaskJson>> ListWorkspaceTasks(string authToken, long workspaceId)
        {
            var url = new Uri(v8Url, string.Format("workspaces/{0}/tasks", workspaceId.ToString()));
            return ListObjects<TaskJson> (authToken, url);
        }

        public Task<TaskJson> UpdateTask(string authToken, TaskJson jsonObject)
        {
            var url = new Uri(v8Url, string.Format("tasks/{0}", jsonObject.RemoteId.Value.ToString()));
            return UpdateObject(authToken, url, jsonObject);
        }

        public Task DeleteTask(string authToken, TaskJson jsonObject)
        {
            var url = new Uri(v8Url, string.Format("tasks/{0}", jsonObject.RemoteId.Value.ToString()));
            return DeleteObject(authToken, url);
        }

        public Task DeleteTasks(string authToken, IEnumerable<TaskJson> jsonObjects)
        {
            var url = new Uri(v8Url, string.Format("tasks/{0}",
                                                   string.Join(",", jsonObjects.Select((json) => json.RemoteId.Value.ToString()))));
            return DeleteObjects(authToken, url);
        }

        #endregion

        #region Time entry methods

        public Task<TimeEntryJson> CreateTimeEntry(string authToken, TimeEntryJson jsonObject)
        {
            var url = new Uri(v8Url, "time_entries");
            jsonObject.CreatedWith = String.Format("{0}-obm-{1}", Platform.DefaultCreatedWith, OBMExperimentManager.ExperimentNumber);
            return CreateObject(authToken, url, jsonObject);
        }

        public Task<TimeEntryJson> GetTimeEntry(string authToken, long id)
        {
            var url = new Uri(v8Url, string.Format("time_entries/{0}", id));
            return GetObject<TimeEntryJson> (authToken, url);
        }

        public Task<List<TimeEntryJson>> ListTimeEntries(string authToken)
        {
            var url = new Uri(v8Url, "time_entries");
            return ListObjects<TimeEntryJson> (authToken, url);
        }

        public Task<List<TimeEntryJson>> ListTimeEntries(string authToken, DateTime start, DateTime end)
        {
            return ListTimeEntries(authToken, start, end, CancellationToken.None);
        }

        public Task<List<TimeEntryJson>> ListTimeEntries(string authToken, DateTime start, DateTime end, CancellationToken cancellationToken)
        {
            var url = new Uri(v8Url,
                              string.Format("time_entries?start_date={0}&end_date={1}",
                                            WebUtility.UrlEncode(start.ToUtc().ToString("o")),
                                            WebUtility.UrlEncode(end.ToUtc().ToString("o"))));
            return ListObjects<TimeEntryJson> (authToken, url, cancellationToken);
        }

        public Task<List<TimeEntryJson>> ListTimeEntries(string authToken, DateTime end, int days)
        {
            return ListTimeEntries(authToken, end, days, CancellationToken.None);
        }

        public Task<List<TimeEntryJson>> ListTimeEntries(string authToken, DateTime end, int days, CancellationToken cancellationToken)
        {
            var url = new Uri(v8Url,
                              string.Format("time_entries?end_date={0}&num_of_days={1}",
                                            WebUtility.UrlEncode(end.ToUtc().ToString("o")),
                                            days));
            return ListObjects<TimeEntryJson> (authToken, url, cancellationToken);
        }

        public Task<TimeEntryJson> UpdateTimeEntry(string authToken, TimeEntryJson jsonObject)
        {
            var url = new Uri(v8Url, string.Format("time_entries/{0}", jsonObject.RemoteId.Value.ToString()));
            return UpdateObject(authToken, url, jsonObject);
        }

        public Task DeleteTimeEntry(string authToken, TimeEntryJson jsonObject)
        {
            var url = new Uri(v8Url, string.Format("time_entries/{0}", jsonObject.RemoteId.Value.ToString()));
            return DeleteObject(authToken, url);
        }

        #endregion

        #region Workspace methods

        public Task<WorkspaceJson> CreateWorkspace(string authToken, WorkspaceJson jsonObject)
        {
            var url = new Uri(v8Url, "workspaces");
            return CreateObject(authToken, url, jsonObject);
        }

        public Task<WorkspaceJson> GetWorkspace(string authToken, long id)
        {
            var url = new Uri(v8Url, string.Format("workspaces/{0}", id.ToString()));
            return GetObject<WorkspaceJson> (authToken, url);
        }

        public Task<List<WorkspaceJson>> ListWorkspaces(string authToken)
        {
            var url = new Uri(v8Url, "workspaces");
            return ListObjects<WorkspaceJson> (authToken, url);
        }

        public Task<WorkspaceJson> UpdateWorkspace(string authToken, WorkspaceJson jsonObject)
        {
            var url = new Uri(v8Url, string.Format("workspaces/{0}", jsonObject.RemoteId.Value.ToString()));
            return UpdateObject(authToken, url, jsonObject);
        }

        #endregion

        #region Tag methods

        public Task<TagJson> CreateTag(string authToken, TagJson jsonObject)
        {
            var url = new Uri(v8Url, "tags");
            return CreateObject(authToken, url, jsonObject);
        }

        public Task<TagJson> UpdateTag(string authToken, TagJson jsonObject)
        {
            var url = new Uri(v8Url, string.Format("tags/{0}", jsonObject.RemoteId.Value.ToString()));
            return UpdateObject(authToken, url, jsonObject);
        }

        public Task DeleteTag(string authToken, TagJson jsonObject)
        {
            var url = new Uri(v8Url, string.Format("tags/{0}", jsonObject.RemoteId.Value.ToString()));
            return DeleteObject(authToken, url);
        }

        public async Task<List<TagJson>> GetTagsSince(string authToken, DateTime? since)
        {
            since = null;
            var httpReq = GetV9SinceRequest(authToken, "me/tags", since);
            var httpResp = await SendAsync(httpReq).ConfigureAwait(false);
            var respData = await httpResp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var wrap = JsonConvert.DeserializeObject<List<TagJson>> (respData);
            return wrap;
        }

        #endregion

        #region Workspace user methods

        public async Task<WorkspaceUserJson> CreateWorkspaceUser(string authToken, WorkspaceUserJson jsonObject)
        {
            var url = new Uri(v8Url, string.Format("workspaces/{0}/invite", jsonObject.WorkspaceRemoteId.ToString()));

            var json = JsonConvert.SerializeObject(new
            {
                emails = new string[] { jsonObject.Email },
            });
            var httpReq = SetupRequest(authToken, new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = url,
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
            var httpResp = await SendAsync(httpReq).ConfigureAwait(false);
            var wrap = JObject.Parse(await httpResp.Content.ReadAsStringAsync().ConfigureAwait(false));
            var data = wrap ["data"] [0].ToObject<WorkspaceUserJson> ();
            return data;
        }

        public Task<WorkspaceUserJson> UpdateWorkspaceUser(string authToken, WorkspaceUserJson jsonObject)
        {
            var url = new Uri(v8Url, string.Format("workspace_users/{0}", jsonObject.RemoteId.Value.ToString()));
            return UpdateObject(authToken, url, jsonObject);
        }

        public Task DeleteWorkspaceUser(string authToken, WorkspaceUserJson jsonObject)
        {
            var url = new Uri(v8Url, string.Format("workspace_users/{0}", jsonObject.RemoteId.Value.ToString()));
            return DeleteObject(authToken, url);
        }

        #endregion

        #region Project user methods

        public Task<ProjectUserJson> CreateProjectUser(string authToken, ProjectUserJson jsonObject)
        {
            var url = new Uri(v8Url, "project_users");
            return CreateObject(authToken, url, jsonObject);
        }

        public Task<ProjectUserJson> UpdateProjectUser(string authToken, ProjectUserJson jsonObject)
        {
            var url = new Uri(v8Url, string.Format("project_users/{0}", jsonObject.RemoteId.Value.ToString()));
            return UpdateObject(authToken, url, jsonObject);
        }

        public Task DeleteProjectUser(string authToken, ProjectUserJson jsonObject)
        {
            var url = new Uri(v8Url, string.Format("project_users/{0}", jsonObject.RemoteId.Value.ToString()));
            return DeleteObject(authToken, url);
        }

        #endregion

        #region User methods

        public Task<UserJson> CreateUser(UserJson jsonObject)
        {
            var url = new Uri(v8Url, jsonObject.GoogleAccessToken != null ? "signups?app_name=toggl_mobile" : "signups");
            jsonObject.CreatedWith = String.Format("{0}-obm-{1}", Platform.DefaultCreatedWith, OBMExperimentManager.ExperimentNumber);
            return CreateObject(string.Empty, url, jsonObject);
        }

        public Task<UserJson> GetUser(long id)
        {
            var url = new Uri(v8Url, "me");
            return GetObject<UserJson> (string.Empty, url);
        }

        public async Task<UserJson> GetUser(string username, string password)
        {
            var url = new Uri(v8Url, "me");

            var httpReq = new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = url,
            };
            httpReq.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.ASCII.GetBytes(
                                               string.Format("{0}:{1}", username, password))));
            var httpResp = await SendAsync(httpReq).ConfigureAwait(false);
            var respData = await httpResp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var wrap = JsonConvert.DeserializeObject<Wrapper<UserJson>> (respData);

            return wrap.Data;
        }

        public async Task<UserJson> GetUser(string googleAccessToken)
        {
            var url = new Uri(v8Url, "me?app_name=toggl_mobile");

            var httpReq = new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = url,
            };
            httpReq.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.ASCII.GetBytes(
                                               string.Format("{0}:{1}", googleAccessToken, "google_access_token"))));
            var httpResp = await SendAsync(httpReq).ConfigureAwait(false);
            var respData = await httpResp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var wrap = JsonConvert.DeserializeObject<Wrapper<UserJson>> (respData);

            return wrap.Data;
        }

        public Task<UserJson> UpdateUser(string authToken, UserJson jsonObject)
        {
            var url = new Uri(v8Url, "me");
            return UpdateObject(authToken, url, jsonObject);
        }

        // TODO: For testing purposes only
        public async Task DeleteUser(string authToken)
        {
            var json = JsonConvert.SerializeObject(new CloseAccountInfo());
            var httpReq = SetupRequest(authToken, new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(v8Url, "me/close_account"),
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
            await SendAsync(httpReq).ConfigureAwait(false);
        }
        #endregion

        public async Task<UserRelatedJson> GetChanges(string authToken, DateTime? since)
        {
            since = since.ToUtc();
            var relUrl = "me?with_related_data=true";
            if (since.HasValue)
            {
                relUrl = string.Format("{0}&since={1}", relUrl, (long)(since.Value - UnixStart).TotalSeconds);
            }
            var url = new Uri(v8Url, relUrl);

            var httpReq = SetupRequest(authToken, new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = url,
            });
            var httpResp = await SendAsync(httpReq).ConfigureAwait(false);
            var respData = await httpResp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var json = JObject.Parse(respData);

            var user = json["data"].ToObject<UserJson> ();
            return new UserRelatedJson()
            {
                Timestamp = UnixStart + TimeSpan.FromSeconds((long)json["since"]),
                User = user,
                Workspaces = GetChangesObjects<WorkspaceJson> (json["data"]["workspaces"]),
                Tags = GetChangesObjects<TagJson> (json["data"]["tags"]),
                Clients = GetChangesObjects<ClientJson> (json["data"]["clients"]),
                Projects = GetChangesObjects<ProjectJson> (json["data"]["projects"]),
                Tasks = GetChangesObjects<TaskJson> (json["data"]["tasks"]),
                TimeEntries = GetChangesTimeEntryObjects(json["data"]["time_entries"], user),
            };
        }

        private IEnumerable<T> GetChangesObjects<T> (JToken json)
        where T : CommonJson, new()
        {
            if (json == null)
            {
                return Enumerable.Empty<T> ();
            }
            return json.ToObject<List<T>> ();
        }

        private IEnumerable<TimeEntryJson> GetChangesTimeEntryObjects(JToken json, UserJson user)
        {
            if (json == null)
            {
                return Enumerable.Empty<TimeEntryJson> ();
            }
            return json.ToObject<List<TimeEntryJson>> ().Select((te) =>
            {
                te.UserRemoteId = user.RemoteId.Value;
                return te;
            });
        }

        public async Task CreateFeedback(string authToken, FeedbackJson jsonObject)
        {
            var url = new Uri(v8Url, "feedback");

            jsonObject.AppVersion = string.Format("{0}/{1}", Platform.AppIdentifier, Platform.AppVersion);
            jsonObject.Timestamp = Time.Now;

            var json = JsonConvert.SerializeObject(jsonObject);
            var httpReq = SetupRequest(authToken, new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = url,
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
            await SendAsync(httpReq).ConfigureAwait(false);
        }

        public async Task CreateExperimentAction(string authToken, ActionJson jsonObject)
        {
            var url = new Uri(v9Url, "obm/actions");
            var json = JsonConvert.SerializeObject(jsonObject);

            var httpReq = SetupRequest(authToken, new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = url,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
            await SendAsync(httpReq).ConfigureAwait(false);
        }

        private HttpRequestMessage GetV9SinceRequest(string authToken, string relUrl, DateTime? since)
        {
            since.ToUtc();
            if (since.HasValue)
            {
                relUrl = string.Format("{0}?since={1}", relUrl, (long)(since.Value - UnixStart).TotalSeconds);
            }
            var url = new Uri(v9Url, relUrl);
            var httpReq = SetupRequest(authToken, new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = url,
            });
            return httpReq;
        }

        private class Wrapper<T>
        {
            [JsonProperty("data")]
            public T Data { get; set; }

            [JsonProperty("since", NullValueHandling = NullValueHandling.Ignore)]
            public long? Since { get; set; }
        }
    }
}
