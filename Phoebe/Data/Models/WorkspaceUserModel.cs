using System;
using System.Linq.Expressions;
using Toggl.Phoebe.Data.DataObjects;

namespace Toggl.Phoebe.Data.Models
{
    public class WorkspaceUserModel : Model<WorkspaceUserData>
    {
        private static string GetPropertyName<T> (Expression<Func<WorkspaceUserModel, T>> expr)
        {
            return expr.ToPropertyName ();
        }

        public static new readonly string PropertyId = Model<WorkspaceUserData>.PropertyId;
        public static readonly string PropertyIsAdmin = GetPropertyName (m => m.IsAdmin);
        public static readonly string PropertyIsActive = GetPropertyName (m => m.IsActive);
        public static readonly string PropertyWorkspace = GetPropertyName (m => m.Workspace);
        public static readonly string PropertyUser = GetPropertyName (m => m.User);

        public WorkspaceUserModel ()
        {
        }

        public WorkspaceUserModel (WorkspaceUserData data) : base (data)
        {
        }

        public WorkspaceUserModel (Guid id) : base (id)
        {
        }

        protected override WorkspaceUserData Duplicate (WorkspaceUserData data)
        {
            return new WorkspaceUserData (data);
        }

        protected override void OnBeforeSave ()
        {
            if (Data.WorkspaceId == Guid.Empty) {
                throw new ValidationException ("Project must be set for ProjectUser model.");
            }
            if (Data.UserId == Guid.Empty) {
                throw new ValidationException ("User must be set for ProjectUser model.");
            }
        }

        protected override void DetectChangedProperties (WorkspaceUserData oldData, WorkspaceUserData newData)
        {
            base.DetectChangedProperties (oldData, newData);
            if (oldData.IsAdmin != newData.IsAdmin) {
                OnPropertyChanged (PropertyIsAdmin);
            }
            if (oldData.IsActive != newData.IsActive) {
                OnPropertyChanged (PropertyIsActive);
            }
            if (oldData.WorkspaceId != newData.WorkspaceId || workspace.IsNewInstance) {
                OnPropertyChanged (PropertyWorkspace);
            }
            if (oldData.UserId != newData.UserId || user.IsNewInstance) {
                OnPropertyChanged (PropertyUser);
            }
        }

        public bool IsAdmin
        {
            get {
                EnsureLoaded ();
                return Data.IsAdmin;
            } set {
                if (IsAdmin == value) {
                    return;
                }

                MutateData (data => data.IsAdmin = value);
            }
        }

        public bool IsActive
        {
            get {
                EnsureLoaded ();
                return Data.IsActive;
            } set {
                if (IsActive == value) {
                    return;
                }

                MutateData (data => data.IsActive = value);
            }
        }

        private ForeignRelation<WorkspaceModel> workspace;
        private ForeignRelation<UserModel> user;

        protected override void InitializeRelations ()
        {
            base.InitializeRelations ();

            workspace = new ForeignRelation<WorkspaceModel> () {
                ShouldLoad = EnsureLoaded,
                Factory = id => new WorkspaceModel (id),
                Changed = m => MutateData (data => data.WorkspaceId = m.Id),
            };

            user = new ForeignRelation<UserModel> () {
                ShouldLoad = EnsureLoaded,
                Factory = id => new UserModel (id),
                Changed = m => MutateData (data => data.UserId = m.Id),
            };
        }

        [ModelRelation]
        public WorkspaceModel Workspace
        {
            get { return workspace.Get (Data.WorkspaceId); }
            set { workspace.Set (value); }
        }

        [ModelRelation]
        public UserModel User
        {
            get { return user.Get (Data.UserId); }
            set { user.Set (value); }
        }

        public static explicit operator WorkspaceUserModel (WorkspaceUserData data)
        {
            if (data == null) {
                return null;
            }
            return new WorkspaceUserModel (data);
        }

        public static implicit operator WorkspaceUserData (WorkspaceUserModel model)
        {
            return model.Data;
        }
    }
}
