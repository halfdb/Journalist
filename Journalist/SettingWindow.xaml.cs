using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private ObservableCollection<string> targetFilters;
        private int targetFilterIndex;
        private ObservableCollection<string> packFilters;
        private int packFilterIndex;
        private ObservableCollection<string> excludedPaths;

        public bool Updated = false;

        private StringCollection StringCollectionFromCollection(Collection<string> collection)
        {
            var value = new StringCollection();
            foreach (var item in collection)
            {
                value.Add(item);
            }
            return value;
        }

        private ObservableCollection<string> ObservableCollectionFromStringCollection(StringCollection collection)
        {
            var value = new ObservableCollection<string>();
            foreach (var item in collection)
            {
                value.Add(item);
            }
            return value;
        }

        public StringCollection TargetFilters
        {
            get => StringCollectionFromCollection(targetFilters);

            set
            {
                TargetFilterCombo.ItemsSource = null;
                targetFilters = ObservableCollectionFromStringCollection(value);
                TargetFilterCombo.ItemsSource = targetFilters;
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
            get => StringCollectionFromCollection(packFilters);
            set
            {
                PackFilterCombo.ItemsSource = null;
                packFilters = ObservableCollectionFromStringCollection(value);
                PackFilterCombo.ItemsSource = packFilters;
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
            get => StringCollectionFromCollection(excludedPaths);
            set
            {
                ExcludedPathList.ItemsSource = null;
                excludedPaths = ObservableCollectionFromStringCollection(value);
                ExcludedPathList.ItemsSource = excludedPaths;
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

        private delegate void AddedAction<T>(T dialog) where T : Window;
        private void AddUsingWindow<T>(AddedAction<T> action) where T : Window
        {
            var dialog = Activator.CreateInstance<T>();
            bool added = dialog.ShowDialog() ?? false;
            if (added)
            {
                action(dialog);
                Updated = true;
            }
        }

        private void AddPackFilterButton_Click(object sender, RoutedEventArgs e)
        {
            AddUsingWindow(new AddedAction<AddPackFilterWindow>((dialog) =>
            {
                var titledFilter = new StringBuilder()
                    .Append(dialog.TitleText.Text)
                    .Append('|')
                    .Append(dialog.FilterText.Text)
                    .ToString();
                packFilters.Add(titledFilter);
                PackFilterIndex = packFilters.Count - 1;
            }));
        }

        private void AddTargetFilterButton_Click(object sender, RoutedEventArgs e)
        {
            AddUsingWindow(new AddedAction<AddTargetFilterWindow>((dialog) =>
            {
                targetFilters.Add(dialog.FilterText.Text);
                TargetFilterIndex = targetFilters.Count - 1;
            }));
        }

        private void AddExcludedPathButton_Click(object sender, RoutedEventArgs e)
        {
            AddUsingWindow(new AddedAction<AddPathWindow>((dialog) =>
            {
                excludedPaths.Add(dialog.PathText.Text);
            }));
        }

        private void RemoveExcludedPathButton_Click(object sender, RoutedEventArgs e)
        {
            excludedPaths.RemoveAt(ExcludedPathList.SelectedIndex);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
