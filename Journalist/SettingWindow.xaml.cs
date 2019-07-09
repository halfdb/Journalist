using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Journalist
{
    /// <summary>
    /// Interaction logic for SettingWindow.xaml
    /// </summary>
    public sealed partial class SettingWindow : Window
    {
        private StringCollection targetFilters;
        private int targetFilterIndex;
        private StringCollection packFilters;
        private int packFilterIndex;
        private StringCollection excludedPaths;

        public bool Updated = false;

        public StringCollection TargetFilters
        {
            get => targetFilters;
            set
            {
                targetFilters = value;
                TargetFilterCombo.ItemsSource = value;
            }
        }

        public int TargetFilterIndex
        {
            get => targetFilterIndex;
            set
            {
                targetFilterIndex = value;
                TargetFilterCombo.SelectedIndex = value;
            }
        }

        public StringCollection PackFilters
        {
            get => packFilters;
            set
            {
                packFilters = value;
                PackFilterCombo.ItemsSource = value;
            }
        }

        public int PackFilterIndex
        {
            get => packFilterIndex;
            set
            {
                packFilterIndex = value;
                PackFilterCombo.SelectedIndex = value;
            }
        }

        public StringCollection ExcludedPaths
        {
            get => excludedPaths;
            set
            {
                excludedPaths = value;
                ExcludedPathList.ItemsSource = value;
            }
        }

        public SettingWindow()
        {
            InitializeComponent();

            if (Application.Current.MainWindow is MainWindow main)
            {
                Title = main.TryResourceString("#SettingsTitle#");
                TargetFilterSettingTitle.Text = main.TryResourceString("#TargetFilterSettingTitle#");
                AddTargetFilterButton.Content = main.TryResourceString("#AddButton#");
                PackFilterSettingTitle.Text = main.TryResourceString("#PackFilterSettingTitle#");
                AddPackFilterButton.Content = main.TryResourceString("#AddButton#");
                ExcludedPathSettingTitle.Text = main.TryResourceString("#ExcludedPathSettingTitle#");
                AddExcludedPathButton.Content = main.TryResourceString("#AddButton#");
                RemoveExcludedPathButton.Content = main.TryResourceString("#RemoveButton#");
                AboutButton.Content = main.TryResourceString("#AboutButton#");
                OkButton.Content = main.TryResourceString("#OkButton#");
            }
        }

        private void ComboSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender == PackFilterCombo)
            {
                PackFilterIndex = PackFilterCombo.SelectedIndex;
            }
            else if (sender == TargetFilterCombo)
            {
                TargetFilterIndex = TargetFilterCombo.SelectedIndex;
            }
            else
            {
                return;
            }
            Updated = true;
        }

        private void AddPackFilterButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddPackFilterWindow();
            bool added = dialog.ShowDialog() ?? false;
            if (added)
            {
                var titledFilter = new StringBuilder()
                    .Append(dialog.TitleText.Text)
                    .Append('|')
                    .Append(dialog.FilterText.Text)
                    .ToString();
                packFilters.Add(titledFilter);
                PackFilterCombo.ItemsSource = packFilters;
                PackFilterIndex = packFilters.Count - 1;
                Updated = true;
            }
        }

        private void AddTargetFilterButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddTargetFilterWindow();
            bool added = dialog.ShowDialog() ?? false;
            if (added)
            {
                targetFilters.Add(dialog.FilterText.Text);
                TargetFilterCombo.ItemsSource = targetFilters;
                TargetFilterIndex = targetFilters.Count - 1;
                Updated = true;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
