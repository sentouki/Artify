using System;
using System.Windows;
using System.Windows.Input;
using Artify.ViewModels;
using Artify.Views.misc;
using CefSharp;
using Microsoft.Win32;

namespace Artify.Views
{
    // ReSharper disable once RedundantExtendsListEntry
    public partial class SettingsPopUp : Window
    {
        private readonly SettingsViewModel _settingsVM = new SettingsViewModel();
        public SettingsPopUp()
        {
            InitializeComponent();
            Opacity = 0;
            DataContext = _settingsVM;
            Browser.RequestHandler = new CefRequestHandler();
            Browser.BrowserSettings.ApplicationCache = CefState.Disabled;
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

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // make sure that input contains only numbers
            if (!int.TryParse(e.Text, out _) || string.IsNullOrWhiteSpace(e.Text))
                e.Handled = true;
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
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
