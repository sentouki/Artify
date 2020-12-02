using System.Threading.Tasks;
using ArtAPI;
using Artify.Models;

namespace Artify.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private readonly ArtifyModel _artifyModel;
        public string UserName { get; set; }
        public string UserPassword { private get; set; }
        public bool IsLoginInputValid { get; set; } = true;
        public bool IsLoginButtonEnabled { get; set; } = true;
        public string LoginNotification { get; set; }
        public string SaveLocation
        {
            get => _artifyModel.SavePath;
            set => _artifyModel.SavePath = value;
        }
        public int ClientTimeout
        {
            get => _artifyModel.Platform.ClientTimeout;
            set => _artifyModel.Platform.ClientTimeout = value;
        }
        public int DownloadAttempts
        {
            get => _artifyModel.Platform.DownloadAttempts;
            set => _artifyModel.Platform.DownloadAttempts = value;
        }
        public int ConcurrentTasks
        {
            get => _artifyModel.Platform.ConcurrentTasks;
            set => _artifyModel.Platform.ConcurrentTasks = value;
        }

        public bool IsLoggedIn { get; set; }
        public RelayCommand LoginCommand { get; }
        public SettingsViewModel(ArtifyModel artifyM)
        {
            _artifyModel = artifyM;
            _artifyModel.Platform.LoginStatusChanged += Platform_LoginStatusChanged;
            Platform_LoginStatusChanged(this, new LoginStatusChangedEventArgs(_artifyModel.Platform.LoginState));
            LoginCommand = new RelayCommand(async () => await Login());
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
                    IsLoginButtonEnabled = false;
                    LoginNotification = "Logging in ...";
                    break;
                case LoginStatus.Authenticating:
                    IsLoginButtonEnabled = false;
                    LoginNotification = "Authenticating ...";
                    break;
                case LoginStatus.LoggedIn:
                    LoginNotification = "";
                    IsLoggedIn = true;
                    break;
                case LoginStatus.Failed:
                    LoginNotification = "Authenticating failed";
                    IsLoginButtonEnabled = true;
                    break;
            }
        }

        private async Task Login()
        {
            if (string.IsNullOrEmpty(UserName) || string.IsNullOrEmpty(UserPassword)) return;
            IsLoginButtonEnabled = false;
            if (await _artifyModel.Platform.Login(UserName, UserPassword) is { } result)
            {
                _artifyModel.settings.pixiv_refresh_token = result;
                _artifyModel.UpdateSettings();
            }
            else
            {
                IsLoginInputValid = false;
                LoginNotification = "Incorrect username or password";
            }
            UserPassword = null;
            IsLoginButtonEnabled = true;
        }
    }
}
