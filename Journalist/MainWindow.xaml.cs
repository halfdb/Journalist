using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Forms;

namespace Journalist
{
    public partial class MainWindow : Window
    {
        private const int TIMEOUT = 1000;
        private readonly string APP_NAME;
        private bool cancelExit = true;

        private NotifyIcon NotifyIcon = null;
        public MainWindow()
        {
            SetLanguageDictionary();
            APP_NAME = TryResourceString("#AppName#");
            InitializeComponent();
            InitializeNotifyIcon();
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

            NotifyIcon = new NotifyIcon
            {
                Text = APP_NAME,
                BalloonTipIcon = ToolTipIcon.Info,
                BalloonTipTitle = APP_NAME,
                BalloonTipText = TryResourceString("#RunningBackground#"),
                Icon = new System.Drawing.Icon(@".\Resources\Journalist.ico"),
                ContextMenu = contextMenu,
                Visible = true,
            };
            NotifyIcon.Click += new EventHandler(NotifyIconClick);
        }

        private void NotifyIconClick(object sender, EventArgs e)
        {
            if (sender != NotifyIcon)
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
                NotifyIcon.ShowBalloonTip(TIMEOUT);
            }
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            NotifyIcon.Visible = false;
            base.OnClosed(e);
        }
    }
}
