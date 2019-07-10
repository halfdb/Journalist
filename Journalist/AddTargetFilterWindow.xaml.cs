using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Journalist
{
    /// <summary>
    /// Interaction logic for AddTargetFilterWindow.xaml
    /// </summary>
    public partial class AddTargetFilterWindow : Window
    {
        public AddTargetFilterWindow()
        {
            InitializeComponent();
            if (Application.Current.MainWindow is MainWindow main)
            {
                Title = main.TryResourceString("#AddTargetFilterTitle#");
                FilterTitle.Text = main.TryResourceString("#FilterTitle#");
                OkButton.Content = main.TryResourceString("#OkButton#");
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void FilterText_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OkButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }
    }
}
