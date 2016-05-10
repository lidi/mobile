using System;
using XPlatUtils;

namespace Toggl.Phoebe.Logging
{
    public class BaseLogger : ILogger
    {
        private readonly LogLevel threshold;
        private readonly LogLevel consoleThreshold;

        public BaseLogger()
        {
#if DEBUG
            threshold = LogLevel.Debug;
            consoleThreshold = LogLevel.Debug;
#else
            threshold = LogLevel.Info;
            consoleThreshold = LogLevel.Warning;
#endif
        }

        protected BaseLogger(LogLevel threshold)
        {
            this.threshold = threshold;
        }

        private void Process(LogLevel level, string tag, string message, Exception exc = null)
        {
            LogToConsole(level, tag, message, exc);
            LogToFile(level, tag, message, exc);
            LogToLoggerClient(level, tag, message, exc);
        }

        private void LogToConsole(LogLevel level, string tag, string message, Exception exc)
        {
            if (level < consoleThreshold)
            {
                return;
            }

            WriteConsole(level, tag, message, exc);
        }

        private void LogToFile(LogLevel level, string tag, string message, Exception exc)
        {
            var logStore = ServiceContainer.Resolve<LogStore> ();
            if (logStore != null)
            {
                logStore.Record(level, tag, message, exc);
            }
        }

        private void LogToLoggerClient(LogLevel level, string tag, string message, Exception exc)
        {
            if (level == LogLevel.Error)
            {
                ErrorSeverity severity = ErrorSeverity.Error;

                var md = new Metadata();
                md.AddToTab("Logger", "Tag", tag);
                md.AddToTab("Logger", "Message", message);
                AddExtraMetadata(md);

                var loggerClient = ServiceContainer.Resolve<ILoggerClient> ();
                if (loggerClient != null)
                {
                    loggerClient.Notify(exc, severity, md);
                }
            }
        }

        protected void AddExtraMetadata(Metadata md)
        {
            // TODO: RX Better way to do that!
            // TODO RX: logger. The condition is because Error ends up calling AppState.Setting
            // which is not initialised yet

            if (Reactive.StoreManager.Singleton == null)
                return;

            var settings = Reactive.StoreManager.Singleton.AppState.Settings;
            md.AddToTab("State", "Experiment", OBMExperimentManager.ExperimentNumber);
            md.AddToTab("State", "Push registered", string.IsNullOrWhiteSpace(settings.PushToken) ? "No" : "Yes");

            md.AddToTab("Settings", "Show projects for new", settings.ChooseProjectForNew ? "Yes" : "No");
            md.AddToTab("Settings", "Idle notifications", settings.IdleNotification ? "Yes" : "No");
            md.AddToTab("Settings", "Add default tag", settings.UseDefaultTag ? "Yes" : "No");
            md.AddToTab("Settings", "Is Grouped", settings.GroupedEntries ? "Yes" : "No");
        }

        protected virtual void WriteConsole(LogLevel level, string tag, string message, Exception exc)
        {
            Console.WriteLine("[{1}] {0}: {2}", level, tag, message);
            if (exc != null)
            {
                Console.WriteLine(exc.ToString());
            }
        }

        public void Debug(string tag, string message)
        {
            if (threshold > LogLevel.Debug)
            {
                return;
            }
            Process(LogLevel.Debug, tag, message);
        }

        public void Debug(string tag, string message, object arg0)
        {
            if (threshold > LogLevel.Debug)
            {
                return;
            }
            Process(LogLevel.Debug, tag, string.Format(message, arg0));
        }

        public void Debug(string tag, string message, object arg0, object arg1)
        {
            if (threshold > LogLevel.Debug)
            {
                return;
            }
            Process(LogLevel.Debug, tag, string.Format(message, arg0, arg1));
        }

        public void Debug(string tag, string message, object arg0, object arg1, object arg2)
        {
            if (threshold > LogLevel.Debug)
            {
                return;
            }
            Process(LogLevel.Debug, tag, string.Format(message, arg0, arg1, arg2));
        }

        public void Debug(string tag, string message, params object[] args)
        {
            if (threshold > LogLevel.Debug)
            {
                return;
            }
            Process(LogLevel.Debug, tag, string.Format(message, args));
        }

        public void Debug(string tag, Exception exc, string message)
        {
            if (threshold > LogLevel.Debug)
            {
                return;
            }
            Process(LogLevel.Debug, tag, message, exc);
        }

        public void Debug(string tag, Exception exc, string message, object arg0)
        {
            if (threshold > LogLevel.Debug)
            {
                return;
            }
            Process(LogLevel.Debug, tag, string.Format(message, arg0), exc);
        }

        public void Debug(string tag, Exception exc, string message, object arg0, object arg1)
        {
            if (threshold > LogLevel.Debug)
            {
                return;
            }
            Process(LogLevel.Debug, tag, string.Format(message, arg0, arg1), exc);
        }

        public void Debug(string tag, Exception exc, string message, object arg0, object arg1, object arg2)
        {
            if (threshold > LogLevel.Debug)
            {
                return;
            }
            Process(LogLevel.Debug, tag, string.Format(message, arg0, arg1, arg2), exc);
        }

        public void Debug(string tag, Exception exc, string message, params object[] args)
        {
            if (threshold > LogLevel.Debug)
            {
                return;
            }
            Process(LogLevel.Debug, tag, string.Format(message, args), exc);
        }

        public void Info(string tag, string message)
        {
            if (threshold > LogLevel.Info)
            {
                return;
            }
            Process(LogLevel.Info, tag, message);
        }

        public void Info(string tag, string message, object arg0)
        {
            if (threshold > LogLevel.Info)
            {
                return;
            }
            Process(LogLevel.Info, tag, string.Format(message, arg0));
        }

        public void Info(string tag, string message, object arg0, object arg1)
        {
            if (threshold > LogLevel.Info)
            {
                return;
            }
            Process(LogLevel.Info, tag, string.Format(message, arg0, arg1));
        }

        public void Info(string tag, string message, object arg0, object arg1, object arg2)
        {
            if (threshold > LogLevel.Info)
            {
                return;
            }
            Process(LogLevel.Info, tag, string.Format(message, arg0, arg1, arg2));
        }

        public void Info(string tag, string message, params object[] args)
        {
            if (threshold > LogLevel.Info)
            {
                return;
            }
            Process(LogLevel.Info, tag, string.Format(message, args));
        }

        public void Info(string tag, Exception exc, string message)
        {
            if (threshold > LogLevel.Info)
            {
                return;
            }
            Process(LogLevel.Info, tag, message, exc);
        }

        public void Info(string tag, Exception exc, string message, object arg0)
        {
            if (threshold > LogLevel.Info)
            {
                return;
            }
            Process(LogLevel.Info, tag, string.Format(message, arg0), exc);
        }

        public void Info(string tag, Exception exc, string message, object arg0, object arg1)
        {
            if (threshold > LogLevel.Info)
            {
                return;
            }
            Process(LogLevel.Info, tag, string.Format(message, arg0, arg1), exc);
        }

        public void Info(string tag, Exception exc, string message, object arg0, object arg1, object arg2)
        {
            if (threshold > LogLevel.Info)
            {
                return;
            }
            Process(LogLevel.Info, tag, string.Format(message, arg0, arg1, arg2), exc);
        }

        public void Info(string tag, Exception exc, string message, params object[] args)
        {
            if (threshold > LogLevel.Info)
            {
                return;
            }
            Process(LogLevel.Info, tag, string.Format(message, args), exc);
        }

        public void Warning(string tag, string message)
        {
            if (threshold > LogLevel.Warning)
            {
                return;
            }
            Process(LogLevel.Warning, tag, message);
        }

        public void Warning(string tag, string message, object arg0)
        {
            if (threshold > LogLevel.Warning)
            {
                return;
            }
            Process(LogLevel.Warning, tag, string.Format(message, arg0));
        }

        public void Warning(string tag, string message, object arg0, object arg1)
        {
            if (threshold > LogLevel.Warning)
            {
                return;
            }
            Process(LogLevel.Warning, tag, string.Format(message, arg0, arg1));
        }

        public void Warning(string tag, string message, object arg0, object arg1, object arg2)
        {
            if (threshold > LogLevel.Warning)
            {
                return;
            }
            Process(LogLevel.Warning, tag, string.Format(message, arg0, arg1, arg2));
        }

        public void Warning(string tag, string message, params object[] args)
        {
            if (threshold > LogLevel.Warning)
            {
                return;
            }
            Process(LogLevel.Warning, tag, string.Format(message, args));
        }

        public void Warning(string tag, Exception exc, string message)
        {
            if (threshold > LogLevel.Warning)
            {
                return;
            }
            Process(LogLevel.Warning, tag, message, exc);
        }

        public void Warning(string tag, Exception exc, string message, object arg0)
        {
            if (threshold > LogLevel.Warning)
            {
                return;
            }
            Process(LogLevel.Warning, tag, string.Format(message, arg0), exc);
        }

        public void Warning(string tag, Exception exc, string message, object arg0, object arg1)
        {
            if (threshold > LogLevel.Warning)
            {
                return;
            }
            Process(LogLevel.Warning, tag, string.Format(message, arg0, arg1), exc);
        }

        public void Warning(string tag, Exception exc, string message, object arg0, object arg1, object arg2)
        {
            if (threshold > LogLevel.Warning)
            {
                return;
            }
            Process(LogLevel.Warning, tag, string.Format(message, arg0, arg1, arg2), exc);
        }

        public void Warning(string tag, Exception exc, string message, params object[] args)
        {
            if (threshold > LogLevel.Warning)
            {
                return;
            }
            Process(LogLevel.Warning, tag, string.Format(message, args), exc);
        }

        public void Error(string tag, string message)
        {
            if (threshold > LogLevel.Error)
            {
                return;
            }
            Process(LogLevel.Error, tag, message);
        }

        public void Error(string tag, string message, object arg0)
        {
            if (threshold > LogLevel.Error)
            {
                return;
            }
            Process(LogLevel.Error, tag, string.Format(message, arg0));
        }

        public void Error(string tag, string message, object arg0, object arg1)
        {
            if (threshold > LogLevel.Error)
            {
                return;
            }
            Process(LogLevel.Error, tag, string.Format(message, arg0, arg1));
        }

        public void Error(string tag, string message, object arg0, object arg1, object arg2)
        {
            if (threshold > LogLevel.Error)
            {
                return;
            }
            Process(LogLevel.Error, tag, string.Format(message, arg0, arg1, arg2));
        }

        public void Error(string tag, string message, params object[] args)
        {
            if (threshold > LogLevel.Error)
            {
                return;
            }
            Process(LogLevel.Error, tag, string.Format(message, args));
        }

        public void Error(string tag, Exception exc, string message)
        {
            if (threshold > LogLevel.Error)
            {
                return;
            }
            Process(LogLevel.Error, tag, message, exc);
        }

        public void Error(string tag, Exception exc, string message, object arg0)
        {
            if (threshold > LogLevel.Error)
            {
                return;
            }
            Process(LogLevel.Error, tag, string.Format(message, arg0), exc);
        }

        public void Error(string tag, Exception exc, string message, object arg0, object arg1)
        {
            if (threshold > LogLevel.Error)
            {
                return;
            }
            Process(LogLevel.Error, tag, string.Format(message, arg0, arg1), exc);
        }

        public void Error(string tag, Exception exc, string message, object arg0, object arg1, object arg2)
        {
            if (threshold > LogLevel.Error)
            {
                return;
            }
            Process(LogLevel.Error, tag, string.Format(message, arg0, arg1, arg2), exc);
        }

        public void Error(string tag, Exception exc, string message, params object[] args)
        {
            if (threshold > LogLevel.Error)
            {
                return;
            }
            Process(LogLevel.Error, tag, string.Format(message, args), exc);
        }
    }
}
