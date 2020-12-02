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

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext != null)
            { ((dynamic)DataContext).UserPassword = ((PasswordBox)sender).Password; }
        }

        private void LoginButtonClick(object sender, RoutedEventArgs e)
        {
            if (username.Text.Length == 0) username.Tag = false;
            if (password.Password.Length == 0) password.Tag = false;
        }

        private void InputFieldGotFocus(object sender, RoutedEventArgs e)
        {
            username.Tag = password.Tag = true;
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape) Close();
        }

        private void TextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            // make sure that input contains only numbers
            if (!int.TryParse(e.Text, out _) || string.IsNullOrWhiteSpace(e.Text))
                e.Handled = true;
        }

        private void TextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // make sure that input doesn't contain any whitespace
            switch (e.Key)
            {
                case Key.Space:
                case Key.V when (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                    e.Handled = true;
                    break;
            }
        }
    }
}
