﻿using System;
using System.Collections.Generic;
using System.Timers;
using Cirrious.FluentLayouts.Touch;
using CoreGraphics;
using Foundation;
using NotificationCenter;
using Toggl.Emma.Views;
using UIKit;

namespace Toggl.Emma
{
    [Register ("WidgetViewController")]
    public class WidgetViewController : UIViewController, INCWidgetProviding
    {
        public static string MillisecondsKey = "milliseconds_key";
        public static string TimeEntriesKey = "time_entries_key";
        public static string StartedEntryKey = "started_entry_key";
        public static string IsUserLoggedKey = "is_logged_key";
        public static string AppActiveEntryKey = "app_active_entry_key";
        public static string AppBackgroundEntryKey = "app_bg_entry_key";

        public static string TodayUrlPrefix = "today";
        public static string StartEntryUrlPrefix = "start";
        public static string ContinueEntryUrlPrefix = "continue";

        private NSUserDefaults nsUserDefaults;

        public NSUserDefaults UserDefaults
        {
            get {
                if (nsUserDefaults == null) {
                    nsUserDefaults = new NSUserDefaults ("group.com.toggl.timer", NSUserDefaultsType.SuiteName);
                }
                return nsUserDefaults;
            }
        }

        private Timer timer;

        private Timer Timer
        {
            get {
                if (timer == null)
                    timer = new Timer {
                    Interval = 1000,
                };
                return timer;
            }
        }

        private bool widgetDisabled; // avoid double interaction

        private bool WidgetDisabled
        {
            get {
                return widgetDisabled;
            }

            set {
                if (widgetDisabled == value) {
                    return;
                }
                widgetDisabled = value;
                tableView.UserInteractionEnabled = !widgetDisabled;
            }
        }

        private StartStopBtn openAppBtn;
        private UIView openAppView;
        private UITableView tableView;

        private nfloat cellHeight = 60;
        private nfloat height;
        private nfloat marginTop;
        private bool isRunning;
        private bool isUserLogged;
        private bool isAppActive;
        private bool isAppOnBackground;

        readonly TableDataSource source = new TableDataSource ();
        readonly List<WidgetEntryData> entries = new List<WidgetEntryData> ();

        public override void LoadView ()
        {
            base.LoadView ();

            isUserLogged = UserDefaults.BoolForKey (IsUserLoggedKey);
            isAppActive = UserDefaults.BoolForKey (AppActiveEntryKey);
            isAppOnBackground = UserDefaults.BoolForKey (AppBackgroundEntryKey);

            marginTop = (isUserLogged && isAppActive) ? 10f : 1f;
            height = (isUserLogged && isAppActive) ? 250f : 62f; // 4 x 60f(cells),

            var v = new UIView {
                BackgroundColor = UIColor.Clear,
                Frame = new CGRect (0,0, UIScreen.MainScreen.Bounds.Width, height),
            };

            v.Add (tableView = new UITableView {
                TranslatesAutoresizingMaskIntoConstraints = false,
                BackgroundColor = UIColor.Clear,
                TableFooterView = new UIView(),
                ScrollEnabled = false,
                RowHeight = cellHeight,
            });

            v.Add (openAppView = new UIView {
                TranslatesAutoresizingMaskIntoConstraints = false,
                Hidden = true,
            });

            UIView bg;
            openAppView.Add (bg = new UIView {
                TranslatesAutoresizingMaskIntoConstraints = false,
                BackgroundColor = UIColor.Black,
                Alpha = 0.1f,
            });

            UILabel textView;
            openAppView.Add (textView = new UILabel {
                TranslatesAutoresizingMaskIntoConstraints = false,
                Font = UIFont.FromName ("Helvetica", 13f),
                Text = isAppActive ? "NoLoggedUser".Tr() : "NoActiveApp".Tr(),
                TextColor = UIColor.White,
                BackgroundColor = UIColor.Clear,
            });

            openAppView.Add (openAppBtn = new StartStopBtn {
                TranslatesAutoresizingMaskIntoConstraints = false,
                IsActive = true,
            });

            openAppView.AddConstraints (

                bg.AtTopOf (openAppView),
                bg.AtLeftOf (openAppView),
                bg.AtRightOf (openAppView),
                bg.AtBottomOf (openAppView),

                textView.WithSameCenterY (openAppView),
                textView.AtLeftOf (openAppView, 50f),
                textView.WithSameHeight (openAppView),
                textView.AtRightOf (openAppView),

                openAppBtn.Width().EqualTo (35f),
                openAppBtn.Height().EqualTo (35f),
                openAppBtn.AtRightOf (openAppView, 15f),
                openAppBtn.WithSameCenterY (openAppView),

                null
            );

            v.AddConstraints (

                tableView.AtTopOf (v),
                tableView.WithSameWidth (v),
                tableView.Height().EqualTo (height - marginTop).SetPriority (UILayoutPriority.DefaultLow),
                tableView.AtBottomOf (v),

                openAppView.AtTopOf (v),
                openAppView.WithSameWidth (v),
                openAppView.Height().EqualTo (cellHeight),

                null
            );

            View = v;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            const string prefix = "com.toggl.timer";

            source.OnStartStop += (sender, e) => {
                if (WidgetDisabled) {
                    return;
                }
                WidgetDisabled = true;
                UIApplication.SharedApplication.OpenUrl (new NSUrl (prefix + "://" + TodayUrlPrefix + "/" + StartEntryUrlPrefix));
            };

            source.OnContinue += (sender, e) => {
                if (WidgetDisabled) {
                    return;
                }
                WidgetDisabled = true;
                var id = string.IsNullOrEmpty (source.SelectedCellId) ? new Guid().ToString() : source.SelectedCellId;
                UserDefaults.SetString (id, StartedEntryKey);
                UIApplication.SharedApplication.OpenUrl (new NSUrl (prefix + "://" + TodayUrlPrefix + "/" + ContinueEntryUrlPrefix));
            };

            source.Controller = this;
            tableView.Source = source;
            tableView.RegisterClassForCellReuse (typeof (WidgetCell), WidgetCell.WidgetProjectCellId);
            tableView.Delegate = new TableViewDelegate ();

            openAppBtn.TouchUpInside += (sender, e) => {
                if (WidgetDisabled) {
                    return;
                }
                WidgetDisabled = true;
                openAppBtn.IsActive = false;
                UIApplication.SharedApplication.OpenUrl (new NSUrl (prefix + "://" + TodayUrlPrefix + "/"));
            };

            UpdateContent();

            UpdateTimeValue ();

            if (isRunning) {
                // Start to check time
                Timer.Elapsed += (sender, e) => InvokeOnMainThread (UpdateTimeValue);
                Timer.Start();
            }
        }

        public override void ViewDidUnload ()
        {
            Timer.Stop ();
            base.ViewDidUnload ();
        }

        private void UpdateTimeValue()
        {
            // Periodically update content from UserDefaults
            var timeValue = UserDefaults.StringForKey (MillisecondsKey);

            if (!string.IsNullOrEmpty (timeValue)) {
                var cell = (WidgetCell)tableView.CellAt (NSIndexPath.FromRowSection (0, 0));
                if (cell != null) {
                    cell.TimeValue = isAppOnBackground ? string.Empty : UserDefaults.StringForKey (MillisecondsKey);
                }
            }
        }

        private void UpdateContent()
        {
            // Check if user is logged
            if (!isUserLogged || !isAppActive) {
                tableView.Hidden = true;
                openAppView.Hidden = false;
                return;
            }

            isRunning = false;
            WidgetDisabled = false;

            // Get saved entries
            var timeEntryJson = UserDefaults.StringForKey (TimeEntriesKey);
            if (timeEntryJson != string.Empty) {
                entries.Clear();
                entries.AddRange ( Newtonsoft.Json.JsonConvert.DeserializeObject<List<WidgetEntryData>> (timeEntryJson));
            }

            if (entries.Count > 0) {

                // Check running state
                foreach (var item in entries) {
                    isRunning = isRunning || item.IsRunning;
                }

                if (!isRunning) {
                    // Add empty cell at top
                    var emptyEntry = new WidgetEntryData { IsEmpty = true };
                    entries.Insert (0, emptyEntry);
                    entries.RemoveAt (entries.Count - 1);
                }

            } else {
                entries.Add (new WidgetEntryData { IsEmpty = true });
            }

            tableView.ReloadData();
        }

        [Export ("widgetPerformUpdateWithCompletionHandler:")]
        public void WidgetPerformUpdate (Action<NCUpdateResult> completionHandler)
        {
            UpdateContent();
            UpdateTimeValue ();
            completionHandler (NCUpdateResult.NewData);
        }

        [Export ("widgetMarginInsetsForProposedMarginInsets:")]
        public UIEdgeInsets GetWidgetMarginInsets (UIEdgeInsets defaultMarginInsets)
        {
            defaultMarginInsets.Left = 0f;
            defaultMarginInsets.Bottom = 0f;
            defaultMarginInsets.Top = marginTop;
            return defaultMarginInsets;
        }

        internal class TableDataSource : UITableViewSource
        {
            public WidgetViewController Controller;

            public event EventHandler OnContinue;

            public event EventHandler OnStartStop;

            public string SelectedCellId { get; set; }

            public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
            {
                var cell = (WidgetCell) tableView.DequeueReusableCell (WidgetCell.WidgetProjectCellId, indexPath);
                cell.TranslatesAutoresizingMaskIntoConstraints = false;
                cell.Data = Controller.entries[ indexPath.Row];

                cell.StartBtnPressed += (sender, e) => {
                    if (indexPath.Row == 0 && OnStartStop != null) {
                        OnStartStop.Invoke (this, new EventArgs());
                    } else if (OnContinue != null) {
                        SelectedCellId = Controller.entries[ indexPath.Row].Id;
                        OnContinue.Invoke (this, new EventArgs());
                    }
                };
                return cell;
            }

            public override nint RowsInSection (UITableView tableview, nint section)
            {
                return (nint)Controller.entries.Count;
            }
        }

        internal class TableViewDelegate : UITableViewDelegate
        {
            int maxCellNum = 3;
            nfloat borderMargin = 50;

            public override void WillDisplay (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath)
            {
                cell.BackgroundColor = UIColor.Clear;

                if (cell.RespondsToSelector (new ObjCRuntime.Selector ("setSeparatorInset:"))) {
                    var separator = cell.SeparatorInset;
                    separator.Left = (indexPath.Row < maxCellNum) ? borderMargin : tableView.Bounds.Width;
                    cell.SeparatorInset = separator;
                }

                if (cell.RespondsToSelector (new ObjCRuntime.Selector ("setPreservesSuperviewLayoutMargins:"))) {
                    cell.PreservesSuperviewLayoutMargins = false;
                }

                if (cell.RespondsToSelector (new ObjCRuntime.Selector ("setLayoutMargins:"))) {
                    cell.LayoutMargins = UIEdgeInsets.Zero;
                }
            }
        }
    }
}