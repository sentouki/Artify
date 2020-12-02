using System;
using System.Diagnostics;
using ArtAPI;
using Artify.Models;
using Artify.ViewModels.misc;

namespace Artify.ViewModels
{
    public class ArtifyViewModel : BaseViewModel
    {
        private readonly ArtifyModel artifyModel;
        private State _currentState;
        #region public properties
        public string RunDownloadButtonContent { get; set; } = "Download";
        public int DownloadProgress { get; private set; }
        public int TotalImageCount { get; private set; } = 1;
        public string UserInput { get; set; }
        public bool RunDLButtonIsEnabled { get; private set; } = true;
        public bool IsLoggedIn { get; set; } = true;
        public string Notification { get; set; }
        public string InputErrorMessage { get; set; }
        public bool IsInputValid { get; set; } = true;
        public string SelectedPlatform { get; set; }
        #endregion
        #region commands
        public RelayCommand RunDownloadCommand { get; private set; }
        public RelayCommand<string> SelectPlatformCommand { get; }
        public RelayCommand<IShutDown> ShutdownCommand { get; }
        #endregion
        #region ctor
        public ArtifyViewModel()
        {
            artifyModel = new ArtifyModel();
            RunDownloadCommand = new RelayCommand(RunDownload);
            SelectPlatformCommand = new RelayCommand<string>(SelectPlatform);
            ShutdownCommand = new RelayCommand<IShutDown>(Shutdown);
        }
        #endregion
        private void Shutdown(IShutDown view)
        {
            artifyModel.UpdateSettings();
            CancelDownload();
            view.AppShutDown(_currentState);
        }

        public SettingsViewModel CreateSettingsVM()
        {
            return new SettingsViewModel(artifyModel);
        }
        #region ArtAPI event handler
        private void Platform_DownloadStateChanged(object sender, DownloadStateChangedEventArgs e)
        {
            _currentState = e.state;
            switch (e.state)
            {
                case State.DownloadPreparing:
                    UserInput = null;
                    Notification = "Preparing download...";
                    break;
                case State.DownloadRunning:
                    TotalImageCount = e.TotalImageCount;
                    Notification = $"Downloading {e.TotalImageCount} images";
                    RunDownloadCommand = new RelayCommand(CancelDownload);
                    RunDownloadButtonContent = "Cancel";
                    RunDLButtonIsEnabled = true;
                    break;
                case State.DownloadCompleted:
                    RunDownloadCommand = new RelayCommand(RunDownload);
                    RunDownloadButtonContent = "Download";
                    Notification = "Completed";
                    if (e.FailedDownloads > 0)
                        Notification += $", {e.FailedDownloads} images couldn't be downloaded";
                    break;
                case State.DownloadCanceled:
                    RunDownloadCommand = new RelayCommand(RunDownload);
                    RunDownloadButtonContent = "Download";
                    Notification = "Canceled";
                    if (e.ExceptionMsg != null)
                        Notification += ": " + e.ExceptionMsg;
                    if (e.FailedDownloads > 0)
                        Notification += $"\n{e.FailedDownloads} images couldn't be downloaded";
                    break;
                case State.ExceptionRaised:
#if DEBUG
                    Debug.WriteLine($"ERR_MSG: {e.ExceptionMsg}");
#endif
                    break;
            }
        }
        private void UpdateProgress(object sender, DownloadProgressChangedEventArgs e)
        {
            DownloadProgress = e.CurrentProgress;
        }
        private void PlatformOnLoginStatusChanged(object sender, LoginStatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case LoginStatus.LoggingIn:
                    RunDLButtonIsEnabled = false;
                    Notification = "Logging in ...";
                    break;
                case LoginStatus.Authenticating:
                    Notification = "Authenticating ...";
                    RunDLButtonIsEnabled = false;
                    break;
                case LoginStatus.LoggedIn:
                    Notification = "";
                    IsLoggedIn = true;
                    RunDLButtonIsEnabled = true;
                    break;
                case LoginStatus.Failed:
                    Notification = "Authenticating failed";
                    IsLoggedIn = false;
                    RunDLButtonIsEnabled = true;
                    break;
            }
        }
        #endregion
        #region methods
        private async void SelectPlatform(string platform)
        {
            if (_currentState == State.DownloadRunning)
            {
                CancelDownload();
            }
            SelectedPlatform = platform;
            artifyModel.SelectPlatform(platform);
            artifyModel.Platform.DownloadProgressChanged += UpdateProgress;
            artifyModel.Platform.DownloadStateChanged += Platform_DownloadStateChanged;
            artifyModel.Platform.LoginStatusChanged += PlatformOnLoginStatusChanged;
            IsLoggedIn = await artifyModel.Auth();
        }

        private async void RunDownload()
        #region download method
        {
            DownloadProgress = 0;
            RunDLButtonIsEnabled = false;
#if DEBUG
            var watch = new Stopwatch();
            watch.Start();
#endif
            try
            {
                switch (await artifyModel.CheckUserInput(UserInput))
                {
                    case InputType.Empty:
                        InputErrorMessage = "Input field cannot be empty!";
                        IsInputValid = false;
                        break;
                    case InputType.Wrong:
                        InputErrorMessage = "Wrong URL";
                        IsInputValid = false;
                        break;
                    case InputType.ArtistNotFound:
                        InputErrorMessage = "Artist not found";
                        IsInputValid = false;
                        break;
                    case InputType.URL:
                        await artifyModel.Platform.GetImagesAsync(UserInput);
                        break;
                    case InputType.ArtistNameOrID:
                        await artifyModel.Platform.GetImagesAsync(await artifyModel.Platform.CreateUrlFromName(UserInput));
                        break;
                }
            }
            catch (System.Net.Http.HttpRequestException e) when (e.InnerException is System.Net.Sockets.SocketException)
            {
                InputErrorMessage = "No internet connection";
                Notification = "";
            }
            catch (Exception e)
            {
                InputErrorMessage = "Something went wrong :(";
                Notification = e.Message;
            }
            finally
            {
                RunDLButtonIsEnabled = true;
#if DEBUG
                watch.Stop();
                Notification += $"\n{watch.Elapsed}";
#endif
            }
        }
        #endregion

        private void CancelDownload()
        {
            artifyModel.Platform?.CancelDownload();
        }
        #endregion
    }
}
