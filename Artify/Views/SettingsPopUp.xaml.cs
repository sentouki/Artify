using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Artify.Views
{
    public partial class SettingsPopUp : Window
    {
        public SettingsPopUp(Window owner)
        {
            InitializeComponent();
            DataContext = owner.DataContext;
            Owner = owner;
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

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext != null)
            { ((dynamic)DataContext).UserPassword = ((PasswordBox)sender).Password; }
        }

        private void LoginButtonClick(object sender, RoutedEventArgs e)
        {
            if (username.Text.Length == 0 ||password.Password.Length == 0)
            {
                if (username.Text.Length == 0) username.Tag = false;
                if (password.Password.Length == 0) password.Tag = false;
                return;
            }
            LoginButton.IsEnabled = false;
        }

        private void InputFieldGotFocus(object sender, RoutedEventArgs e)
        {
            username.Tag = password.Tag = LoginButton.IsEnabled = true;
        }
    }
}
