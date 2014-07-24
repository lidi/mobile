﻿using System;
using System.Collections.Generic;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Phoebe.Data.Json.Converters
{
    public sealed class TimeEntryJsonConverter : BaseJsonConverter
    {
        public TimeEntryJson Export (IDataStoreContext ctx, TimeEntryData data)
        {
            var userId = GetRemoteId<UserData> (ctx, data.UserId);
            var workspaceId = GetRemoteId<WorkspaceData> (ctx, data.WorkspaceId);
            var projectId = GetRemoteId<ProjectData> (ctx, data.ProjectId);
            var taskId = GetRemoteId<TaskData> (ctx, data.TaskId);
            var tags = GetTimeEntryTags (ctx, data.Id);

            return new TimeEntryJson () {
                Id = data.RemoteId,
                ModifiedAt = data.ModifiedAt.ToUtc (),
                Description = data.Description,
                IsBillable = data.IsBillable,
                StartTime = data.StartTime.ToUtc (),
                StopTime = data.StopTime.ToUtc (),
                DurationOnly = data.DurationOnly,
                Duration = EncodeDuration (data),
                Tags = tags,
                UserId = userId,
                WorkspaceId = workspaceId,
                ProjectId = projectId,
                TaskId = taskId,
            };
        }

        private List<string> GetTimeEntryTags (IDataStoreContext ctx, Guid id)
        {
            if (id == Guid.Empty)
                return new List<string> (0);
            return ctx.GetTimeEntryTagNames (id);
        }

        private static long EncodeDuration (TimeEntryData data)
        {
            var now = Time.UtcNow;

            // Calculate time entry duration
            TimeSpan duration;
            if (data.StartTime == DateTime.MinValue) {
                duration = TimeSpan.Zero;
            } else {
                duration = (data.StopTime ?? now) - data.StartTime;
                if (duration < TimeSpan.Zero) {
                    duration = TimeSpan.Zero;
                }
            }

            // Encode the duration
            var encoded = (long)duration.TotalSeconds;
            if (data.State == TimeEntryState.Running) {
                encoded = (long)(encoded - now.ToUnix ().TotalSeconds);
            }
            return encoded;
        }

        private static void DecodeDuration (TimeEntryData data, TimeEntryJson json)
        {
            // Decode duration:
            TimeSpan duration;
            if (json.Duration < 0) {
                data.State = TimeEntryState.Running;
                duration = Time.UtcNow.ToUnix () + TimeSpan.FromSeconds (json.Duration);
            } else {
                data.State = TimeEntryState.Finished;
                duration = TimeSpan.FromSeconds (json.Duration);
            }

            // Set start and stop times based on the duration:
            var now = Time.UtcNow;
            if (data.State == TimeEntryState.Finished) {
                data.StartTime = json.StartTime.ToUtc ();
                data.StopTime = json.StartTime.ToUtc () + duration;
            } else {
                data.StartTime = now - duration;
                data.StopTime = null;
            }
        }

        private static Guid GetUserLocalId (IDataStoreContext ctx, long id)
        {
            if (id == 0) {
                var authManager = ServiceContainer.Resolve<AuthManager> ();
                if (authManager.User == null) {
                    throw new ArgumentException ("Cannot import TimeEntry with missing user when no authenticated user.", "id");
                }
                return authManager.User.Id;
            }
            return GetLocalId<UserData> (ctx, id);
        }

        private static void Merge (IDataStoreContext ctx, TimeEntryData data, TimeEntryJson json)
        {
            var userId = GetUserLocalId (ctx, json.UserId);
            var workspaceId = GetLocalId<WorkspaceData> (ctx, json.WorkspaceId);
            var projectId = GetLocalId<ProjectData> (ctx, json.ProjectId);
            var taskId = GetLocalId<TaskData> (ctx, json.TaskId);

            data.Description = json.Description;
            data.IsBillable = json.IsBillable;
            data.DurationOnly = json.DurationOnly;
            data.UserId = userId;
            data.WorkspaceId = workspaceId;
            data.ProjectId = projectId;
            data.TaskId = taskId;
            DecodeDuration (data, json);

            MergeCommon (data, json);
        }

        private static void ResetTags (IDataStoreContext ctx, TimeEntryData timeEntryData, TimeEntryJson json)
        {
            // Don't touch the tags when the field is null
            if (json.Tags == null) {
                return;
            }

            var con = ctx.Connection;

            // Resolve tags to IDs:
            var tagIds = new List<Guid> ();
            foreach (var tagName in json.Tags) {
                // Prevent importing empty (invalid) tags:
                if (String.IsNullOrWhiteSpace (tagName))
                    continue;

                var id = ctx.GetTagIdFromName (timeEntryData.WorkspaceId, tagName);

                if (id == Guid.Empty) {
                    // Need to create a new tag:
                    var tagData = new TagData () {
                        Name = tagName,
                        WorkspaceId = timeEntryData.WorkspaceId,
                    };
                    con.Insert (tagData);

                    id = timeEntryData.Id;
                }

                tagIds.Add (id);
            }

            // Iterate over TimeEntryTags and determine which to keep and which to discard:
            var inters = con.Table<TimeEntryTagData> ().Where (m => m.TimeEntryId == timeEntryData.Id);
            var toDelete = new List<TimeEntryTagData> ();
            foreach (var inter in inters) {
                if (tagIds.Contains (inter.TagId)) {
                    tagIds.Remove (inter.TagId);
                } else {
                    toDelete.Add (inter);
                }
            }

            // Delete unused tags intermediate rows:
            foreach (var inter in toDelete) {
                ctx.Delete (inter);
            }

            // Create new intermediate rows:
            foreach (var tagId in tagIds) {
                ctx.Put (new TimeEntryTagData () {
                    TagId = tagId,
                    TimeEntryId = timeEntryData.Id,
                });
            }
        }

        public TimeEntryData Import (IDataStoreContext ctx, TimeEntryJson json, Guid? localIdHint = null, bool forceUpdate = false)
        {
            var data = GetByRemoteId<TimeEntryData> (ctx, json.Id.Value, localIdHint);

            if (json.DeletedAt.HasValue) {
                if (data != null) {
                    // TODO: Delete TimeEntryTag intermediate data
                    ctx.Delete (data);
                    data = null;
                }
            } else if (data == null || forceUpdate || data.ModifiedAt.ToUtc () < json.ModifiedAt.ToUtc ()) {
                data = data ?? new TimeEntryData ();
                Merge (ctx, data, json);
                data = ctx.Put (data);
                // Also update tags from the JSON we are merging:
                ResetTags (ctx, data, json);
            }

            return data;
        }
    }
}
