using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Forms;

namespace Journalist
{
    public partial class MainWindow : Window
    {
        public string AppName { get; }
        protected const int ballonTimeout = 1000;

        protected bool cancelExit = true;
        protected NotifyIcon notifyIcon = null;

        private const int blurRadius = 5;
        protected void ShowLogin(bool show=true)
        {
            if (show)
            {
                MainContent.IsEnabled = false;
                MainContentBlur.Radius = blurRadius;
                Cover.Visibility = Visibility.Visible;
            }
            else
            {
                MainContent.IsEnabled = true;
                MainContentBlur.Radius = 0;
                Cover.Visibility = Visibility.Hidden;
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            Cover.IsEnabled = false;
            LoginProgress.Visibility = Visibility.Visible;

            var phone = PhoneText.Text;
            var password = PasswordText.Password;
            if (phone.Length != 0 && password.Length != 0)
            {
                if (sender != this)
                {
                    SavedPhone = phone;
                    SavedPassword = password;
                }

                client.LoginAsync(phone, password);
            }
            else
            {
                Cover.IsEnabled = true;
                LoginProgress.Visibility = Visibility.Hidden;
            }
        }

        internal string TryResourceString(string key)
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
                (object sender, EventArgs e) => { Activate(); }
                ));
            contextMenu.MenuItems.Add(new MenuItem(
                TryResourceString("#ShowJobWindow#"),
                (object sender, EventArgs e) => { OpenJobWindow(); }
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
            else
            {
                foreach (var window in App.Current.Windows)
                {
                    if (window == this)
                    {
                        continue;
                    }
                    (window as Window)?.Close();
                }
            }
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            notifyIcon.Visible = false;
            base.OnClosed(e);
        }

        private void DirectoryText_LostFocus(object sender, RoutedEventArgs e)
        {
            if (watchingPath != DirectoryText.Text)
            {
                watchingPath = DirectoryText.Text;
                RestartWatching();
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                var result = dialog.ShowDialog();
                if (result != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }
                watchingPath = dialog.SelectedPath;
                DirectoryText.Text = watchingPath;
                RestartWatching();
            }
        }

        private void SetSelectedJob(Site.Job job)
        {
            if (client != null)
            {
                client.SelectedJobId = job?.Id;
            }
            Properties.Settings.Default.SelectedJobId = job?.Id ?? -1;
            Properties.Settings.Default.Save();
        }

        private void JobCombo_SelectionChanged(object sender, RoutedEventArgs e)
        {
            // item 0 is placeholder
            if (JobCombo.SelectedItem is Site.Job job)
            {
                SetSelectedJob(job);
            }
        }

        protected JobWindow CurrentJobWindow = null;
        protected void OpenJobWindow()
        {
            void callback(Site.Job job)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var idx = JobCombo.Items.IndexOf(job);
                    if (idx == -1)
                    {
                        idx = 0;
                    }
                    JobCombo.SelectedIndex = idx;
                }));
            }

            void closingCallback()
            {
                CurrentJobWindow = null;
            }

            if (CurrentJobWindow == null)
            {
                // item 0 is placeholder
                CurrentJobWindow = new JobWindow(client.Jobs, JobCombo.SelectedIndex - 1, callback, closingCallback)
                {
                    Title = TryResourceString("#JobTitle#")
                };
                CurrentJobWindow.IdColumn.Header = TryResourceString("#IdHeader#");
                CurrentJobWindow.NameColumn.Header = TryResourceString("#NameHeader#");
                CurrentJobWindow.TypeColumn.Header = TryResourceString("#TypeHeader#");
                CurrentJobWindow.CreationColumn.Header = TryResourceString("#CreationHeader#");
                CurrentJobWindow.ExpireColumn.Header = TryResourceString("#ExpireHeader#");
                CurrentJobWindow.ClassNameColumn.Header = TryResourceString("#ClassNameHeader#");
                CurrentJobWindow.Show();
            }
            CurrentJobWindow?.Activate();
        }

        private void SelectJobButton_Click(object sender, RoutedEventArgs e)
        {
            OpenJobWindow();
        }

        private bool settingWindowOpened = false;
        private void SettingButton_Click(object sender, RoutedEventArgs e)
        {
            if (settingWindowOpened)
            {
                return;
            }
            settingWindowOpened = true;
            var settingWindow = new SettingWindow()
            {
                TargetFilters = targetFilters,
                TargetFilterIndex = targetFilterIndex,
                PackFilters = packTitledFilters,
                PackFilterIndex = packFilterIndex,
                ExcludedPaths = excludedPaths
            };
            settingWindow.Updated = false;
            settingWindow.Closed += new EventHandler((object _s, EventArgs _e) =>
            {
                if (_s is SettingWindow window)
                {
                    if (window.Updated)
                    {
                        targetFilters = window.TargetFilters;
                        targetFilterIndex = window.TargetFilterIndex;
                        packTitledFilters = window.PackFilters;
                        packFilterIndex = window.PackFilterIndex;
                        excludedPaths = window.ExcludedPaths;

                        Properties.Settings.Default.TargetFilters = targetFilters;
                        Properties.Settings.Default.TargetFilterIndex = targetFilterIndex;
                        Properties.Settings.Default.PackTitledFilters = packTitledFilters;
                        Properties.Settings.Default.PackFilterIndex = packFilterIndex;
                        Properties.Settings.Default.ExcludedPaths = excludedPaths;
                        Properties.Settings.Default.Save();

                        RestartWatching();
                    }
                    settingWindowOpened = false;
                }
            });
            settingWindow.Show();
            settingWindow.Activate();
        }
    }
}
