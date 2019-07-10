using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Journalist
{
    /// <summary>
    /// Interaction logic for AddPackFilterWindow.xaml
    /// </summary>
    public partial class AddPackFilterWindow : Window
    {
        public AddPackFilterWindow()
        {
            InitializeComponent();

            if (App.Current.MainWindow is MainWindow main)
            {
                Title = main.TryResourceString("#AddPackFilterTitle#");
                TitleTitle.Text = main.TryResourceString("#TitleTitle#");
                FilterTitle.Text = main.TryResourceString("#FilterTitle#");
                OkButton.Content = main.TryResourceString("#OkButton#");
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void TextKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OkButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }
    }
}
