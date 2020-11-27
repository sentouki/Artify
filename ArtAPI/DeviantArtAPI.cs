using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ArtAPI
{
    public sealed class DeviantArtAPI : RequestArt
    {
        private const string
            ClientID = "12774",
            ClientSecret = "597114f315705b9624c7c1d74ad729e1",
            AUTH_URL = "https://www.deviantart.com/oauth2/token",
            GALLERY_URL = "https://www.deviantart.com/api/v1/oauth2/gallery/all?mature_content=true&limit=20&username={0}&offset=",
            ORIGINIMAGE_URL = "https://www.deviantart.com/api/v1/oauth2/deviation/download/{0}?mature_content=true";

        private const int Offset = 20;

        public bool IsLoggedIn { get; private set; }

        private readonly Regex _scaledDownImgUrlPattern = new Regex(@"(w_[0-9]{3,4}),(h_[0-9]{3,4}),(q_[0-9]{2})", RegexOptions.Compiled);

        public override Task<Uri> CreateUrlFromName(string artistName)
        {
            return Task.FromResult(new Uri($"https://www.deviantart.com/{artistName}"));
        }

        public override async Task<bool> CheckArtistExistsAsync(string artistName)
        {
            var response = await Client.GetAsync($"https://www.deviantart.com/{artistName}");
            return response.IsSuccessStatusCode;
        }

        public override async Task GetImagesAsync(Uri artistUrl)
        {
            if (!IsLoggedIn)
            {
                OnDownloadStateChanged(new DownloadStateChangedEventArgs(State.DownloadCanceled, "not logged in"));
                return;
            }
            OnDownloadStateChanged(new DownloadStateChangedEventArgs(State.DownloadPreparing));
            var artistName = artistUrl?.AbsolutePath.Split('/')[1];
            if (artistName == null) return;
            CreateSaveDir(artistName);
            await GetImagesMetadataAsync(string.Format(GALLERY_URL, artistName)).ConfigureAwait(false);
            await DownloadImagesAsync().ConfigureAwait(false);
        }

        protected override async Task GetImagesMetadataAsync(string apiUrl)
        {
            var paginationOffset = 0;

            while (true)
            {
                var rawResponse = await Client.GetStringAsync(apiUrl + paginationOffset).ConfigureAwait(false);
                var responseJson = JObject.Parse(rawResponse);
                var Gallery = (JContainer)responseJson["results"];
                if (!(Gallery.HasValues)) return;     // check if the user has any images in his gallery
                var tasks = Gallery.Select(async (image) =>
                {
                    if (image["content"] == null) return;
                    var deviationID = image["deviationid"].ToString();
                    var url = image["content"]["src"].ToString();
                    if (_scaledDownImgUrlPattern.IsMatch(url))                     // Check if it's a URL to a scaled down image in order to avoid unnecessary api calls (too many calls may result in a 'Error 401 API token expired') 
                    {
                        if (await GetOriginImage(deviationID) is { } _url)   // try to get the origin image, use the scaled down image if fails
                            url = _url;
                    }
                    lock (ImagesToDownload)
                    {
                        ImagesToDownload.Add(new ImageModel()
                        {
                            Url = url,
                            Name = image["title"].ToString(),
                            ID = deviationID,
                            FileType = image["content"]["src"].ToString().Split('?')[0].Split('/').Last()
                                .Split('.')[1] // maybe not the best way but surely the the easiest one
                        });
                    }
                });
                await Task.WhenAll(tasks);
                if (responseJson["has_more"].ToString() == "False") return;
                paginationOffset += Offset;
            }
        }
        /// <summary>
        /// get the Url to the origin image, not the scaled down one
        /// </summary>
        private async Task<string> GetOriginImage(string deviationID)
        {
            try
            {
                var rawResponse = await Client.GetStringAsync(string.Format(ORIGINIMAGE_URL, deviationID))
                                                    .ConfigureAwait(false);
                var responseJson = JObject.Parse(rawResponse);
                return responseJson["src"].ToString();
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        public override async Task<bool> auth(string refreshToken)
        {
            var data = new Dictionary<string, string>()
            {
                {"grant_type", "client_credentials" },
                {"client_id", ClientID},
                {"client_secret", ClientSecret}
            };
            using var content = new FormUrlEncodedContent(data);
            try
            {
                var response = await Client.PostAsync(AUTH_URL, content).ConfigureAwait(false);
                var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                if (jsonResponse["status"].ToString() == "error") return false;
                if (Client.DefaultRequestHeaders.Contains("Authorization"))
                    Client.DefaultRequestHeaders.Remove("Authorization");
                Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {jsonResponse["access_token"]}");
            }
            catch (HttpRequestException)
            {
                return false;
            }
            return IsLoggedIn = true;
        }
    }
}
