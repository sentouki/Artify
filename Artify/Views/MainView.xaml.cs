using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using ArtAPI;
using Artify.ViewModels;
using Artify.ViewModels.misc;

namespace Artify.Views
{
    public partial class MainWindow : Window, IShutDown
    {
        private Storyboard
            UnBlurAnimation,
            BlurAnimation,
            HideSelectionMenuAnimation,
            ShowSelectionMenuAnimation;

        private SettingsPopUp popUp;
        private readonly ArtifyViewModel _artifyVM = new ArtifyViewModel();

        public MainWindow()
        {
            InitializeComponent();
            UnBlurAnimation = FindResource("UnBlurAnimation") as Storyboard;
            BlurAnimation = FindResource("BlurAnimation") as Storyboard;
            HideSelectionMenuAnimation = (FindResource("HideSelectionMenuAnimation") as Storyboard);
            ShowSelectionMenuAnimation = (FindResource("ShowSelectionMenuAnimation") as Storyboard);
            SetupAnimations();
            SetupEventHandlers();
            MaxHeight = SystemParameters.WorkArea.Height + 10;
            MaxWidth = SystemParameters.WorkArea.Width + 10;
            Opacity = 0;
            DataContext = _artifyVM;
            ResizeButton.DataContext = this;
        }

        private void SetupEventHandlers()
        {
            HideSelectionMenuAnimation.Completed += HideSelectionMenuAnimation_Completed;
        }
        #region click and keys events

        private void SelectionMenuButton_Click(object sender, RoutedEventArgs e)
        {
            HideSelectionMenu();
        }
        private void SettingsClick(object sender, RoutedEventArgs e)
        {
            popUp = new SettingsPopUp
            {
                Owner = this,
                DataContext = _artifyVM.CreateSettingsVM()
            };
            popUp.ShowDialog();
        }

        #region MainWindow Events
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void ResizeButton_Click(object sender, RoutedEventArgs e)
        {
            ResizeMode = WindowState == WindowState.Normal ? ResizeMode.NoResize : ResizeMode.CanResize;
            WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if ((e.Key == System.Windows.Input.Key.Escape) && SelectionMenu.Visibility == Visibility.Hidden)
            {
                ShowSelectionMenu();
            }
            else if ((e.Key == System.Windows.Input.Key.Escape) && SelectionMenu.Visibility == Visibility.Visible && SelectedPlatformIcon.Source != null)
            {
                HideSelectionMenu();
            }
        }
        private void SelectedPlatformIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowSelectionMenu();
        }

        private void WindowChromeOnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            ResizeMode = ResizeMode.NoResize;
            DragMove();
        }

        private void WindowChromeMouseUp(object sender, MouseButtonEventArgs e)
        {
            ResizeMode = ResizeMode.CanResize;
        }
        #endregion
        #endregion

        #region animations
        private void SetupAnimations()
        {
            if (UnBlurAnimation.IsSealed)        // workaround taken from https://bit.ly/2XnNEGN
                UnBlurAnimation = UnBlurAnimation.Clone();
            if (HideSelectionMenuAnimation.IsSealed)
                HideSelectionMenuAnimation = HideSelectionMenuAnimation.Clone();
            if (BlurAnimation.IsSealed)
                BlurAnimation = BlurAnimation.Clone();
            if (ShowSelectionMenuAnimation.IsSealed)
                ShowSelectionMenuAnimation = ShowSelectionMenuAnimation.Clone();
            Storyboard.SetTarget(UnBlurAnimation, MainContent);
            Storyboard.SetTarget(HideSelectionMenuAnimation, SelectionMenu);
            Storyboard.SetTarget(BlurAnimation, MainContent);
            Storyboard.SetTarget(ShowSelectionMenuAnimation, SelectionMenu);
        }
        private void HideSelectionMenu()
        {
            SelectionMenu.IsHitTestVisible = false;
            UnBlurAnimation.Begin();
            HideSelectionMenuAnimation.Begin();
            MainContent.IsHitTestVisible = true;
        }
        private void ShowSelectionMenu()
        {
            MainContent.IsHitTestVisible = false;
            SelectionMenu.Visibility = Visibility.Visible;
            BlurAnimation.Begin();
            ShowSelectionMenuAnimation.Begin();
            SelectionMenu.IsHitTestVisible = true;
        }
        private void UserInputField_GotFocus(object sender, RoutedEventArgs e)
        {
            UserInputField.Tag = true;
            InputValidationLabel.Content = "";
        }

        private void HideSelectionMenuAnimation_Completed(object sender, EventArgs e)
        {
            SelectionMenu.Visibility = Visibility.Hidden;
        }
        #endregion

        private void LoginNotification_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            popUp?.Close();
        }

        public void AppShutDown()
        {
            DataContext = null;
            GC.Collect(2);
            GC.WaitForPendingFinalizers();
            Application.Current.Shutdown();
        }
    }
}