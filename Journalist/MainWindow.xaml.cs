using System;
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
        private const int ballonTimeout = 1000;

        private bool cancelExit = true;
        private NotifyIcon notifyIcon = null;

        public MainWindow()
        {
            SetLanguageDictionary();
            AppName = TryResourceString("#AppName#");
            InitializeComponent();
            InitializeNotifyIcon();

            if (true)
            {
                Console.WriteLine(Directory.GetCurrentDirectory());
                Packer packer = new Packer(new Packer.Config()
                {
                    Path = "./",
                    PackFileNameFilters = new string[] {"*.txt"},
                    TargetFileNameFilter = "*.target"
                });
                using (var f = File.CreateText(@".\g.target"))
                {
                    f.WriteLine("ewgw");
                }
                File.Delete(@".\g.target");
                using (var f = File.CreateText(@".\g.target"))
                {
                    f.WriteLine("jins");
                }
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
