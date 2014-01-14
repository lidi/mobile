﻿using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Toggl.Phoebe.Data;
using XPlatUtils;

namespace Toggl.Phoebe.Net
{
    public class AuthManager : ObservableObject
    {
        private static string GetPropertyName<T> (Expression<Func<AuthManager, T>> expr)
        {
            return expr.ToPropertyName ();
        }

        public AuthManager ()
        {
            var credStore = ServiceContainer.Resolve<ICredentialStore> ();
            try {
                UserId = credStore.UserId;
                Token = credStore.ApiToken;
            } catch (ArgumentException) {
                // When data is corrupt and cannot find user
                credStore.Clear ();
            }
        }

        public async Task<bool> Authenticate (string username, string password)
        {
            if (IsAuthenticated)
                throw new InvalidOperationException ("Cannot authenticate when old credentials still present.");
            if (IsAuthenticating)
                throw new InvalidOperationException ("Another authentication is still in progress.");

            var client = ServiceContainer.Resolve<ITogglClient> ();
            IsAuthenticating = true;

            var user = await client.GetUser (username, password);
            if (user == null)
                return false;

            var credStore = ServiceContainer.Resolve<ICredentialStore> ();
            credStore.UserId = user.Id;
            credStore.ApiToken = user.ApiToken;

            user.IsPersisted = true;
            IsAuthenticating = false;
            UserId = user.Id;
            Token = user.ApiToken;
            IsAuthenticated = true;

            // TODO: Send message about successful authentication

            return true;
        }

        public void Forget ()
        {
            if (IsAuthenticated)
                throw new InvalidOperationException ("Cannot forget credentials which don't exist.");

            var credStore = ServiceContainer.Resolve<ICredentialStore> ();
            credStore.Clear ();

            IsAuthenticated = false;
            Token = null;
            UserId = null;

            // TODO: Clear database
            // TODO: Send message about user logging out
        }

        private bool authenticating;
        public static readonly string PropertyIsAuthenticating = GetPropertyName ((m) => m.IsAuthenticating);

        public bool IsAuthenticating {
            get { return authenticating; }
            private set {
                if (authenticating == value)
                    return;

                ChangePropertyAndNotify (PropertyIsAuthenticating, delegate {
                    authenticating = value;
                });
            }
        }

        private bool authenticated;
        public static readonly string PropertyIsAuthenticated = GetPropertyName ((m) => m.IsAuthenticated);

        public bool IsAuthenticated {
            get { return authenticated; }
            private set {
                if (authenticated == value)
                    return;

                ChangePropertyAndNotify (PropertyIsAuthenticated, delegate {
                    authenticated = value;
                });
            }
        }

        private Guid? userId;
        public static readonly string PropertyUserId = GetPropertyName ((m) => m.UserId);

        public Guid? UserId {
            get { return userId; }
            private set {
                if (userId == value)
                    return;

                UserModel model = null;
                if (value.HasValue) {
                    model = Model.Get<UserModel> (value.Value);
                    if (model == null)
                        throw new ArgumentException ("Unable to resolve UserId to model.");
                }

                ChangePropertyAndNotify (PropertyUserId, delegate {
                    userId = value;
                });

                User = model;
            }
        }

        private UserModel user;
        public static readonly string PropertyUser = GetPropertyName ((m) => m.User);

        public UserModel User {
            get { return user; }
            private set {
                if (user == value)
                    return;
                ChangePropertyAndNotify (PropertyUser, delegate {
                    user = value;
                });
            }
        }

        private string token;
        public static readonly string PropertyToken = GetPropertyName ((m) => m.Token);

        public string Token {
            get { return token; }
            private set {
                if (token == value)
                    return;

                ChangePropertyAndNotify (PropertyToken, delegate {
                    token = value;
                });
            }
        }
    }
}