﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ArtAPI;
using Newtonsoft.Json;

namespace Artify.Models
{
    public enum InputType
    {
        Empty,
        Wrong,
        ArtistNotFound,
        URL,
        ArtistNameOrID,
    }
    /// <summary>
    /// a wrapper singleton around the ArtAPI
    /// </summary>
    public class ArtifyModel
    {
        private readonly string _settingsDirPath, _settingsFilePath;
        private readonly Dictionary<string, Regex> _urlPattern = new Dictionary<string, Regex>()
        {
            {"general", new Regex(@"(https?://)?(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}(\/?[a-zA-Z0-9]*\/?)*") },
            {"artstation", new Regex(@"(https://)?(www\.)?artstation\.com/[0-9a-zA-Z]+/?") },
            {"pixiv", new Regex(@"(https://)?(www\.)?pixiv\.net/[a-z]{0,6}/users/[0-9]+/?") },
            {"deviantart", new Regex(@"(https://)?(www\.)?deviantart\.com/[0-9a-zA-Z]+/?")}
        };
        // container for the classes
        private readonly Dictionary<string, Func<RequestArt>> ArtPlatform = new Dictionary<string, Func<RequestArt>>()
        {
            { "artstation", () => new ArtStationAPI() },
            { "pixiv", () => new PixivAPI() },
            { "deviantart", () => new DeviantArtAPI()}
        };
        private static ArtifyModel _instance;
        public static ArtifyModel Instance => _instance ??= new ArtifyModel();
        private RequestArt _platform;
        public RequestArt Platform
        {
            get => _platform;
            set
            {
                if (_platform?.ToString() != value?.ToString())
                    _platform = value;
            }
        }

        public string SavePath
        {
            get => settings.last_used_savepath ?? Environment.GetFolderPath((Environment.SpecialFolder.MyPictures));  // set the default dir for images
            set => Platform.SavePath = settings.last_used_savepath = value;
        }
        public int ClientTimeout
        {
            get => settings.timeout > 0 ? settings.timeout : Platform.ClientTimeout;
            set => Platform.ClientTimeout = settings.timeout = value;
        }
        public int DownloadAttempts
        {
            get => settings.download_attempts > 0 ? settings.download_attempts : Platform.DownloadAttempts;
            set => Platform.DownloadAttempts = settings.download_attempts = value;
        }
        public int ConcurrentTasks
        {
            get => settings.concurrent_tasks > 0 ? settings.concurrent_tasks : Platform.ConcurrentTasks;
            set => Platform.ConcurrentTasks = settings.concurrent_tasks = value;
        }

        private string _selectedPlatform;
        public Settings settings = new Settings();
        private ArtifyModel()
        {
            _settingsDirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ".artify");
            _settingsFilePath = Path.Combine(_settingsDirPath, "settings.json");
            if (CheckSettingsDir())
                LoadSettings();
            else CreateSettingsDir();
        }

        private bool CheckSettingsDir()
        {
            return Directory.Exists(_settingsDirPath) && File.Exists(_settingsFilePath);
        }
        private void LoadSettings()
        {
            var fi = new FileInfo(_settingsFilePath) { Attributes = FileAttributes.Normal };
            try
            {
                using var sr = new StreamReader(_settingsFilePath);
                var stringJson = sr.ReadToEnd();
                if (stringJson.Length > 0)
                {
                    settings = JsonConvert.DeserializeObject<Settings>(stringJson);
                }
            }
            catch (JsonReaderException)
            {
                CreateSettingsDir();
            }
            fi.Attributes = FileAttributes.ReadOnly | FileAttributes.Hidden;
        }
        private void CreateSettingsDir()
        {
            var di = Directory.CreateDirectory(_settingsDirPath);
            di.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
            File.Create(_settingsFilePath).Close();
        }
        public void UpdateSettings()
        {
            if (Platform == null) return;
            settings.last_used_savepath = Platform.SavePath;
            if (string.IsNullOrWhiteSpace(settings.pixiv_refresh_token))
            {
                settings.pixiv_refresh_token = null;
            }
            var fi = new FileInfo(_settingsFilePath) { Attributes = FileAttributes.Normal };
            using (var sw = new StreamWriter(_settingsFilePath))
            {
                new JsonSerializer().Serialize(sw, settings);
            }
            fi.Attributes = FileAttributes.ReadOnly | FileAttributes.Hidden;
        }

        /// <summary>
        /// check if the input is a valid URL and if so, check if it's the platform's URL
        /// </summary>
        /// <returns>true if it's the right url, false if it's a url but a wrong one, null if neither (e.g. artist name)</returns>
        private bool? CheckUrl(string input)
        {
            if (!_urlPattern["general"].IsMatch(input))
            {
                return null;
            }
            return _urlPattern[_selectedPlatform].IsMatch(input);
        }
        /// <summary>
        /// check the user input
        /// </summary>
        /// <param name="input"> user input like artist url or name</param>
        /// <returns><see cref="InputType"/></returns>
        public async Task<InputType> CheckUserInput(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return InputType.Empty;
            }

            var result = CheckUrl(input);
            if (!(result ?? false) && result.HasValue)
            {
                return InputType.Wrong;
            }
            if (result ?? false)
            {
                return InputType.URL;
            }
            if (!await Platform.CheckArtistExistsAsync(input))
            {
                return InputType.ArtistNotFound;
            }
            return InputType.ArtistNameOrID;
        }

        public void SelectPlatform(string platformName)
        {
            Platform = ArtPlatform[platformName]();  // create an object of the selected platform
            _selectedPlatform = platformName;
            Platform.SavePath = SavePath;
            Platform.ClientTimeout = ClientTimeout;
            Platform.ConcurrentTasks = ConcurrentTasks;
            Platform.DownloadAttempts = DownloadAttempts;
        }

        public async Task<bool> Auth()
        {
            if (_selectedPlatform != "pixiv") return await Platform.Auth(null);
            if (settings.pixiv_refresh_token is { } token && !string.IsNullOrWhiteSpace(token))
            {
                return await Platform.Auth(token);
            }
            return false;
        }

        public async Task PixivLogin(string code)
        {
            var pixiv = (PixivAPI)Platform;
            if (await pixiv.Login(code) is { } result)
            {
                settings.pixiv_refresh_token = result;
                UpdateSettings();
            }
        }
    }
}
