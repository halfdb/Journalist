using System;
using System.Collections.Generic;
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
    }
}
