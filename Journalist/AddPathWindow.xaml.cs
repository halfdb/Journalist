using System.Windows;
using System.Windows.Forms;

namespace Journalist
{
    /// <summary>
    /// Interaction logic for AddPathWindow.xaml
    /// </summary>
    public partial class AddPathWindow : Window
    {
        public AddPathWindow()
        {
            InitializeComponent();

            if (App.Current.MainWindow is MainWindow main)
            {
                Title = main.TryResourceString("#AddPathTitle#");
                PathTitle.Text = main.TryResourceString("#PathTitle#");
                BrowseButton.Content = main.TryResourceString("#BrowseButton#");
                OkButton.Content = main.TryResourceString("#OkButton#");
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
                PathText.Text = dialog.SelectedPath;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
