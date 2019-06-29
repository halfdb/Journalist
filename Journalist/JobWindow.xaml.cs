using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// Interaction logic for JobWindow.xaml
    /// </summary>
    partial class JobWindow : Window
    {
        internal delegate void JobSelected(Site.Job job);
        private JobSelected Callback;
        private Action ClosingCallback;

        internal JobWindow(IList<Site.Job> jobs, int selectedIndex, JobSelected callback, Action closingCallback)
        {
            InitializeComponent();
            JobList.ItemsSource = jobs;
            JobList.SelectedIndex = selectedIndex;

            Callback = callback;
            ClosingCallback = closingCallback;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Callback?.Invoke(JobList.SelectedItem as Site.Job);
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            ClosingCallback?.Invoke();
            base.OnClosing(e);
        }
    }

}
