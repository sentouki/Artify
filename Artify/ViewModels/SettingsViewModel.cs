using System.Threading.Tasks;
using ArtAPI;
using ArtAPI.misc;
using Artify.Models;

namespace Artify.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private readonly ArtifyModel _artifyModel;
        public bool IsInputEnabled { get; set; } = true;
        public bool OpenBrowser { get; set; } = false;
        public string LoginUrl { get; set; }
        public string LoginNotification { get; set; }
        public string SaveLocation
        {
            get => _artifyModel.SavePath;
            set => _artifyModel.SavePath = value;
        }
        public int ClientTimeout
        {
            get => _artifyModel.ClientTimeout;
            set => _artifyModel.ClientTimeout = value > 0 ? value : 1;
        }
        public int DownloadAttempts
        {
            get => _artifyModel.DownloadAttempts;
            set => _artifyModel.DownloadAttempts = value > 0 ? value : 1;
        }
        public int ConcurrentTasks
        {
            get => _artifyModel.ConcurrentTasks;
            set => _artifyModel.ConcurrentTasks = value > 0 ? value : 1;
        }

        public bool IsLoggedIn { get; set; }
        public RelayCommand LoginCommand { get; }
        public SettingsViewModel(ArtifyModel artifyM)
        {
            _artifyModel = artifyM;
            _artifyModel.Platform.LoginStatusChanged += Platform_LoginStatusChanged;
            Platform_LoginStatusChanged(this, new LoginStatusChangedEventArgs(_artifyModel.Platform.LoginState));
            LoginCommand = new RelayCommand(async () => await Login());
            if (_artifyModel.Platform.CurrentState == State.DownloadRunning)
            {
                IsInputEnabled = false;
            }
        }
        ~SettingsViewModel()
        {
            _artifyModel.Platform.LoginStatusChanged -= Platform_LoginStatusChanged;
        }

        private void Platform_LoginStatusChanged(object sender, LoginStatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case LoginStatus.LoggingIn:
                    IsInputEnabled = false;
                    LoginNotification = "Logging in ...";
                    break;
                case LoginStatus.Authenticating:
                    IsInputEnabled = false;
                    LoginNotification = "Authenticating ...";
                    break;
                case LoginStatus.LoggedIn:
                    IsInputEnabled = true;
                    LoginNotification = "";
                    IsLoggedIn = true;
                    break;
                case LoginStatus.Failed:
                    LoginNotification = "Authenticating failed";
                    IsInputEnabled = true;
                    break;
            }
        }

        private async Task Login()
        {
            var pixiv = (PixivAPI) _artifyModel.Platform;
            IsInputEnabled = false;
            LoginUrl = pixiv.Pkce();
            OpenBrowser = true;
            if (await _artifyModel.Platform.Login(UserName, UserPassword) is { } result)
            {
                _artifyModel.settings.pixiv_refresh_token = result;
                _artifyModel.UpdateSettings();
            }
            else
            {
                LoginNotification = "Something went wrong";
            }
            IsInputEnabled = true;
        }
    }
}
