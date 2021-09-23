using ArtAPI;
using ArtAPI.misc;
using Artify.Models;

namespace Artify.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        public bool IsInputEnabled { get; set; } = true;
        public bool OpenBrowser { get; set; }
        public string LoginUrl { get; set; }
        public string SaveLocation
        {
            get => ArtifyModel.Instance.SavePath;
            set => ArtifyModel.Instance.SavePath = value;
        }
        public int ClientTimeout
        {
            get => ArtifyModel.Instance.ClientTimeout;
            set => ArtifyModel.Instance.ClientTimeout = value > 0 ? value : 1;
        }
        public int DownloadAttempts
        {
            get => ArtifyModel.Instance.DownloadAttempts;
            set => ArtifyModel.Instance.DownloadAttempts = value > 0 ? value : 1;
        }
        public int ConcurrentTasks
        {
            get => ArtifyModel.Instance.ConcurrentTasks;
            set => ArtifyModel.Instance.ConcurrentTasks = value > 0 ? value : 1;
        }

        public bool IsLoggedIn { get; set; }
        public RelayCommand LoginCommand { get; }
        public SettingsViewModel()
        {
            ArtifyModel.Instance.Platform.LoginStatusChanged += Platform_LoginStatusChanged;
            Platform_LoginStatusChanged(this, new LoginStatusChangedEventArgs(ArtifyModel.Instance.Platform.LoginState));
            LoginCommand = new RelayCommand(GetCode);
            if (ArtifyModel.Instance.Platform.CurrentState == State.DownloadRunning)
            {
                IsInputEnabled = false;
            }
        }
        ~SettingsViewModel()
        {
            ArtifyModel.Instance.Platform.LoginStatusChanged -= Platform_LoginStatusChanged;
        }

        private void Platform_LoginStatusChanged(object sender, LoginStatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case LoginStatus.LoggingIn:
                    IsInputEnabled = false;
                    break;
                case LoginStatus.Authenticating:
                    IsInputEnabled = false;
                    break;
                case LoginStatus.LoggedIn:
                    IsInputEnabled = true;
                    IsLoggedIn = true;
                    OpenBrowser = false;
                    break;
                case LoginStatus.Failed:
                    IsInputEnabled = true;
                    OpenBrowser = false;
                    break;
            }
        }
        /// <summary>
        /// get the URL with code for the further login process and open the browser
        /// </summary>
        private void GetCode()
        {
            var pixiv = (PixivAPI) ArtifyModel.Instance.Platform;
            LoginUrl = pixiv.Pkce();
            OpenBrowser = true;
        }
    }
}
