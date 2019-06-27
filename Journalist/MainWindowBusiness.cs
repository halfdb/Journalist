using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.IO;
using System.Security.Cryptography;
using System.Security;
using System.Text;

namespace Journalist
{
    public partial class MainWindow : Window
    {
        protected StringCollection packTitledFilters;
        protected int packFilterIndex;
        protected StringCollection targetFilters;
        protected int targetFilterIndex;
        protected string watchingPath;
        private Packer packer;

        public bool PackerReady { get => packer != null; }
        public bool FullWarning { get => packer.PackFull; }

        private const int entropyLength = 32;
        protected byte[] entropy;

        private Client client;

        public bool JobSelectionReady
        {
            get => client?.HasSelectedJob ?? false;
        }

        protected string SavedPhone
        {
            get
            {
                var phoneCipher = Properties.Settings.Default.Phone;
                if (phoneCipher.Length > 0)
                {
                    return Decrypt(phoneCipher);
                }
                else
                {
                    return "";
                }
            }
            set
            {
                Properties.Settings.Default.Phone = Encrypt(value);
                Properties.Settings.Default.Save();
            }
        }

        protected string SavedPassword
        {
            get
            {
                var passwordCipher = Properties.Settings.Default.Password;
                if (passwordCipher.Length > 0)
                {
                    return Decrypt(passwordCipher);
                }
                else
                {
                    return "";
                }
            }

            set
            {
                Properties.Settings.Default.Password = Encrypt(value);
                Properties.Settings.Default.Save();
            }
        }

        private string Encrypt(string plain)
        {
            var plainBytes = Encoding.UTF8.GetBytes(plain);
            var cipher = ProtectedData.Protect(plainBytes, entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(cipher);
        }

        private string Decrypt(string cipher)
        {
            var cipherBytes = Convert.FromBase64String(cipher);
            var plain = ProtectedData.Unprotect(cipherBytes, entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }

        public MainWindow()
        {
            SetLanguageDictionary();
            AppName = TryResourceString("#AppName#");
            InitializeComponent();
            InitializeNotifyIcon();
            InitializeEntropy();

            packTitledFilters = Properties.Settings.Default.PackTitledFilters;
            packFilterIndex = Properties.Settings.Default.PackFilterIndex;
            targetFilters = Properties.Settings.Default.TargetFilters;
            targetFilterIndex = Properties.Settings.Default.TargetFilterIndex;
            watchingPath = Properties.Settings.Default.WatchingPath;
            if (watchingPath.Length == 0)
            {
                watchingPath = TryResourceString("#NotSelected#");
            }

            JobCombo.Items.Add(TryResourceString("#NotSelected#"));
            DirectoryText.Text = watchingPath;

            JobTitle.Text = TryResourceString("#JobTitle#");
            DirectoryTitle.Text = TryResourceString("#DirectoryTitle#");
            UploadTitle.Text = TryResourceString("#UploadTitle#");

            PhoneLabel.Text = TryResourceString("#PhoneLabel#");
            PasswordLabel.Text = TryResourceString("#PasswordLabel#");
            LoginButton.Content = TryResourceString("#LoginButton#");
            BrowseButton.Content = TryResourceString("#BrowseButton#");

            client = new Client(new Site());
            client.AccessCompleted += Client_AccessCompleted;

            PhoneText.Text = SavedPhone;
            PasswordText.Password = SavedPassword;
            LoginButton_Click(this, null);
        }

        private void InitializeEntropy()
        {
            if (!Properties.Settings.Default.IsEntropySet)
            {
                entropy = new byte[entropyLength];
                var rng = new RNGCryptoServiceProvider();
                rng.GetBytes(entropy);
                Properties.Settings.Default.EntropyString = Convert.ToBase64String(entropy);
                Properties.Settings.Default.IsEntropySet = true;
                Properties.Settings.Default.Save();
            }
            else
            {
                entropy = Convert.FromBase64String(Properties.Settings.Default.EntropyString);
            }
        }

        private void Client_AccessCompleted(object sender, Client.AccessEventArgs e)
        {
            switch (e.EventType)
            {
                case Client.EventType.Login:
                    ShowLogin(false);
                    client.GetJobListAsync();
                    RestartWatching();
                    break;
                case Client.EventType.LoginFail:
                    Cover.IsEnabled = true;
                    LoginProgress.Visibility = Visibility.Hidden;

                    notifyIcon.ShowBalloonTip(
                        ballonTimeout,
                        AppName,
                        e.Message,
                        ToolTipIcon.Error
                        );
                    break;
                case Client.EventType.GetJobList:
                    JobCombo.Items.Clear();
                    JobCombo.Items.Add(TryResourceString("#NotSelected#"));
                    var selected = Properties.Settings.Default.SelectedJobId;
                    int idx = 0;
                    foreach (Site.Job job in client.Jobs)
                    {
                        JobCombo.Items.Add(job);
                        if (job.Id == selected)
                        {
                            idx = JobCombo.Items.Count - 1;
                        }
                    }
                    JobCombo.SelectedIndex = idx;
                    break;
                case Client.EventType.UploadJob:
                    Uploading = false;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        notifyIcon.ShowBalloonTip(
                            ballonTimeout,
                            AppName,
                            TryResourceString("#UploadFinished#"),
                            ToolTipIcon.Info
                        );
                    }));

#if !DEBUG
                    File.Delete(ZipFileName);
#endif
                    break;
                case Client.EventType.Fail:
                    notifyIcon.ShowBalloonTip(
                        ballonTimeout,
                        AppName,
                        e.Message,
                        ToolTipIcon.Error
                        );
                    break;
                default:
                    break;
            }
        }

        protected void RestartWatching()
        {
            bool checkPath()
            {
                bool result;
                try
                {
                    watchingPath = Path.GetFullPath(watchingPath);
                }
                catch (Exception e)
                {
                    if (e is ArgumentException
                        || e is ArgumentNullException
                        || e is SecurityException
                        || e is NotSupportedException
                        || e is PathTooLongException)
                    {
                        result = false;
                    }
                    else
                    {
                        throw;
                    }
                }
                result = Directory.Exists(watchingPath);
                if (!result)
                {
                    watchingPath = TryResourceString("#NotSelected#");
                }
                return result;
            }

            if (!checkPath()) { return; }

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

            try
            {
                packer = packer?.ChangeConfig(config) ?? new Packer(config);
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"Error: fail to start packer. Probably a bad watching path. Path: {watchingPath}");
                notifyIcon.ShowBalloonTip(
                        ballonTimeout,
                        AppName,
                        TryResourceString("#FailWatching#"),
                        ToolTipIcon.Error
                        );
                return;
            }

            packer.PackUpdateFinished += PackUpdateFinished;
            packer.StartUpdatingPackFiles();
            Properties.Settings.Default.WatchingPath = watchingPath;
            Properties.Settings.Default.Save();
        }

        public class FileItem
        {
            public string FileName;
            public long Length;
            public DateTime Creation;
            public DateTime LastWrite;
        }
        private string ZipFileName;
        protected bool Uploading = false;
        private void PackUpdateFinished()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var filenameHint = TryResourceString("#FileNameHint#");
                var lengthHint = TryResourceString("#LengthHint#");
                var creationHint = TryResourceString("#CreationHint#");
                var lastWriteHint = TryResourceString("#LastWriteHint#");
                FileList.Items.Clear();
                foreach (var item in packer.PackFileNames)
                {
                    var info = $"{filenameHint}{item} {lengthHint}{new FileInfo(item).Length} {creationHint}{File.GetCreationTime(item)} {lastWriteHint}{File.GetLastWriteTime(item)}";
                    FileList.Items.Add(info);
                }
            }));

            if (FullWarning)
            {
                notifyIcon.ShowBalloonTip(
                    ballonTimeout,
                    AppName,
                    TryResourceString("#FileFullTip#"),
                    ToolTipIcon.Warning
                    );
                return;
            }
            if (!JobSelectionReady)
            {
                return;
            }

            ZipFileName = packer.Pack();
            Console.WriteLine($"Info: {ZipFileName} made");
            if (!Uploading)
            {
                Uploading = true;
                client.UploadJobAsync(ZipFileName);
            }
        }

    }
}
