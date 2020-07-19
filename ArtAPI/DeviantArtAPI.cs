using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
            GALLERY_URL = "https://www.deviantart.com/api/v1/oauth2/gallery/all?mature_content=true&limit=20&username={0}&offset=";

        private const int Offset = 20;

        public bool IsLoggedIn { get; private set; }

        public override Uri CreateUrlFromName(string artistName)
        {
            return new Uri($"https://www.deviantart.com/{artistName}");
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
                foreach (var image in Gallery)
                {
                    if (image["content"] == null) continue;
                    ImagesToDownload.Add(new ImageModel()
                    {
                        Url = image["content"]["src"].ToString(),
                        Name = image["title"].ToString(),
                        ID = image["deviationid"].ToString(),
                        FileType = image["content"]["src"].ToString().Split('?')[0].Split('/').Last().Split('.')[1]  // maybe not the best way but surely the the easiest one
                    });
                }
                if (responseJson["has_more"].ToString() == "False") return;
                paginationOffset += Offset;
            }
        }

        public override async Task<bool> auth(string refreshToken)
        {
            if (IsLoggedIn) return true;
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
            catch(HttpRequestException)
            {
                return false;
            }
            return IsLoggedIn = true;
        }
    }
}
