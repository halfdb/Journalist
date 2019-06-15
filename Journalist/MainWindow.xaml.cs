using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.IO;

namespace Journalist
{
    public partial class MainWindow : Window
    {
        public string AppName { get; }
        protected const int ballonTimeout = 1000;

        protected bool cancelExit = true;
        protected NotifyIcon notifyIcon = null;

        protected StringCollection packTitledFilters;
        protected int packFilterIndex;
        protected StringCollection targetFilters;
        protected int targetFilterIndex;
        protected string watchingPath;
        private Packer packer;

        private bool FullWarning { get => packer.PackFull; }

        public MainWindow()
        {
            SetLanguageDictionary();
            AppName = TryResourceString("#AppName#");
            InitializeComponent();
            InitializeNotifyIcon();

            packTitledFilters = Properties.Settings.Default.PackTitledFilters;
            packFilterIndex = Properties.Settings.Default.PackFilterIndex;
            targetFilters = Properties.Settings.Default.TargetFilters;
            targetFilterIndex = Properties.Settings.Default.TargetFilterIndex;
            watchingPath = Properties.Settings.Default.WatchingPath;

            if (true)  // TODO Log in.
            {
                RestartWatching();
            }
        }

        private string TryResourceString(string key)
        {
            return TryFindResource(key) as string ?? key;
        }

        private void SetLanguageDictionary()
        {
            var dict = new ResourceDictionary();
            var cultureString = Thread.CurrentThread.CurrentCulture.ToString();
            switch (cultureString)
            {
                case "en-US":
                case "zh-CN":
                    dict.Source = new Uri($@".\Resources\Strings.{cultureString}.xaml", UriKind.Relative);
                    break;
                default:
                    dict.Source = new Uri(@".\Resources\Strings.en-US.xaml", UriKind.Relative);
                    break;
            }
            Resources.MergedDictionaries.Add(dict);
        }

        private void InitializeNotifyIcon()
        {
            var contextMenu = new ContextMenu();
            contextMenu.MenuItems.Add(new MenuItem(
                TryResourceString("#ShowWindow#"),
                (object sender, EventArgs e) => { Show(); }
                ));
            contextMenu.MenuItems.Add(new MenuItem(
                TryResourceString("#Exit#"),
                (object sender, EventArgs e) => { cancelExit = false; Close(); }
                ));

            notifyIcon = new NotifyIcon
            {
                Text = AppName,
                BalloonTipIcon = ToolTipIcon.Info,
                BalloonTipTitle = AppName,
                BalloonTipText = TryResourceString("#RunningBackground#"),
                Icon = new System.Drawing.Icon(@".\Resources\Journalist.ico"),
                ContextMenu = contextMenu,
                Visible = true,
            };
            notifyIcon.Click += new EventHandler(NotifyIconClick);
        }

        private void NotifyIconClick(object sender, EventArgs e)
        {
            if (sender != notifyIcon)
            {
                return;
            }
            if (Visibility == Visibility.Hidden)
            {
                Show();
            }
        }

        protected void RestartWatching()
        {
            var packFilters = packTitledFilters
                [packFilterIndex]  // -> "title|filters"
                .Split('|')  // -> ["title", "filters"]
                [1]  // -> "filters"
                .Split(' ');  // -> ["filter1", "filter2"...]
            var config = new Packer.Config()
            {
                Path = watchingPath,
                PackFileNameFilters = packFilters,
                TargetFileNameFilter = targetFilters[targetFilterIndex],
            };

            packer = packer?.ChangeConfig(config) ?? new Packer(config);
            packer.PackUpdateFinished += PackUpdateFinished;
        }

        private void PackUpdateFinished()
        {
            if (FullWarning)
            {
                notifyIcon.ShowBalloonTip(
                    ballonTimeout,
                    AppName,
                    TryResourceString("#FileFullTip#"),
                    ToolTipIcon.Warning
                    );
            }
            string zipFileName = packer.Pack();
            Console.WriteLine($"Info: {zipFileName} made");
            // TODO upload
#if !DEBUG
            File.Delete(zipFileName);
#endif
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (cancelExit)
            {
                e.Cancel = true;
                Hide();
                notifyIcon.ShowBalloonTip(ballonTimeout);
            }
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            notifyIcon.Visible = false;
            base.OnClosed(e);
        }
    }
}
