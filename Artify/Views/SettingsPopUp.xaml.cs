using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace Artify.Views
{
    public partial class SettingsPopUp : Window
    {
        public SettingsPopUp()
        {
            InitializeComponent();
            Opacity = 0;
        }

        private void OpenClick(object sender, RoutedEventArgs e)
        {
            var op = new OpenFileDialog
            {
                Title = "Select a Folder",
                Filter = "Folder | * ",
                ValidateNames = false,
                Multiselect = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "\u00A0",    // workaround to let the user select a directory
                InitialDirectory = Environment.GetFolderPath((Environment.SpecialFolder.MyPictures)),
            };
            if (op.ShowDialog() ?? false)
            {
                path.Content = op.FileName.Replace("\u00A0", string.Empty);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LoginButtonClick(object sender, RoutedEventArgs e)
        {

        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape) Close();
        }
    }
}
