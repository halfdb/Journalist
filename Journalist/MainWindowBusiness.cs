﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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
        protected StringCollection excludedPaths;
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
            excludedPaths = Properties.Settings.Default.ExcludedPaths ?? new StringCollection();

            JobCombo.Items.Add(TryResourceString("#NotSelected#"));
            DirectoryText.Text = watchingPath;

            JobTitle.Text = TryResourceString("#JobTitle#");
            DirectoryTitle.Text = TryResourceString("#DirectoryTitle#");
            UploadTitle.Text = TryResourceString("#UploadTitle#");

            PhoneLabel.Text = TryResourceString("#PhoneLabel#");
            PasswordLabel.Text = TryResourceString("#PasswordLabel#");
            LoginButton.Content = TryResourceString("#LoginButton#");
            SelectJobButton.Content = TryResourceString("#SelectButton#");
            BrowseButton.Content = TryResourceString("#BrowseButton#");
            SettingButton.Content = TryResourceString("#SettingButton#");
            LogoutButton.Content = TryResourceString("#LogoutButton#");

            FileNameColumn.Header = TryResourceString("#FileNameHeader#");
            LengthColumn.Header = TryResourceString("#LengthHeader#");
            LastWriteColumn.Header = TryResourceString("#LastWriteHeader#");

            client = new Client(new Site());
            client.AccessCompleted += Client_AccessCompleted;

            PhoneText.Text = SavedPhone;
            PasswordText.Password = SavedPassword;
            Login(PhoneText.Text, PasswordText.Password);
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

        private void Login(string phone, string password)
        {
            if (phone.Length != 0 && password.Length != 0)
            {
                client.LoginAsync(phone, password);
            }
        }

        private void Logout()
        {
            var oldClient = client;
            client = new Client(new Site());
            client.AccessCompleted += Client_AccessCompleted;
            oldClient.AccessCompleted -= Client_AccessCompleted;
            oldClient.Dispose();
            Uploading = false;
#if !DEBUG
            if (File.Exists(ZipFileName))
            {
                File.Delete(ZipFileName);
            }
#endif
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
                    HoldLoginCover(false);
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
                    Uploading = false;  // if failed to login, it is false already
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
                        return false;
                    }
                    else
                    {
                        throw;
                    }
                }
                return Directory.Exists(watchingPath);
            }

            if (!checkPath())
            {
                watchingPath = TryResourceString("#NotSelected#");
                return;
            }

            var packFilters = packTitledFilters
                [packFilterIndex]  // -> "title|filters"
                .Split('|')  // -> ["title", "filters"]
                [1]  // -> "filters"
                .Split(' ');  // -> ["filter1", "filter2"...]
            var excludedPathList = new List<string>();
            foreach (var item in excludedPaths)
            {
                excludedPathList.Add(item);
            }
            var config = new Packer.Config()
            {
                Path = watchingPath,
                PackFileNameFilters = packFilters,
                TargetFileNameFilter = targetFilters[targetFilterIndex],
                ExcludedPaths = excludedPathList,
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

            FileList.ItemsSource = packer.PackFileNames;
        }

        private string ZipFileName;
        protected bool Uploading = false;
        private void PackUpdateFinished()
        {
            if (!PackerReady)
            {
                return;
            }
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
