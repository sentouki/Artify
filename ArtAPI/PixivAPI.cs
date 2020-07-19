using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ArtAPI
{
    public sealed class PixivAPI : RequestArt
    {
        const string AUTH_URL = @"https://oauth.secure.pixiv.net/auth/token";
        const string LOGIN_SECRET = "28c1fdd170a5204386cb1313c7077b34f83e4aaf4aa829ce78c231e05b0bae2c";
        private const string APIUrlWithLogin = @"https://app-api.pixiv.net/v1/user/illusts?user_id={0}";
        private const string APIUrlWithoutLogin = @"https://www.pixiv.net/touch/ajax/illust/user_illusts?user_id={0}";  // &offset= einbauen

        /// <summary>
        /// one project can contain multiple illustrations
        /// </summary>
        private const string IllustProjectUrl = @"https://www.pixiv.net/touch/ajax/illust/details?illust_id={0}";

        private const string ArtistDetails = @"https://www.pixiv.net/touch/ajax/user/details?id={0}";

        public bool IsLoggedIn { get; private set; }

        public string RefreshToken { get; private set; }
        public PixivAPI()
        {
            Client.DefaultRequestHeaders.Referrer = new Uri("https://www.pixiv.net");
        }

        public override Uri CreateUrlFromName(string username)
        {
            if (int.TryParse(username, out _))
            {
                return CreateUrlFromID(username);
            }
            // NOTE: implement search for artist
            return null;
        }

        public Uri CreateUrlFromID(string userid)
        {
            return new Uri($@"https://www.pixiv.net/en/users/{userid}");
        }

        public override async Task<bool> CheckArtistExistsAsync(string artistID)
        {
            var response = await Client.GetAsync(string.Format(ArtistDetails, artistID))
                 .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        private async Task<string> GetArtistName(string artistID)
        {
            var response = await Client.GetStringAsync(string.Format(ArtistDetails, artistID)).ConfigureAwait(false);
            return JObject.Parse(response)["body"]["user_details"]["user_name"].ToString();
        }

        public override async Task GetImagesAsync(Uri artistUrl)
        {
            OnDownloadStateChanged(new DownloadStateChangedEventArgs(State.DownloadPreparing));
            var artistID = artistUrl?.AbsolutePath.Split('/')[3];
            if (artistID == null) return;
            CreateSaveDir(await GetArtistName(artistID).ConfigureAwait(false));
            if (IsLoggedIn)
            {
                await GetImagesMetadataAsync(string.Format(APIUrlWithLogin, artistID)).ConfigureAwait(false);
            }
            else
            {
                await GetImagesMetadataAsync(string.Format(APIUrlWithoutLogin, artistID)).ConfigureAwait(false);
            }
            await DownloadImagesAsync().ConfigureAwait(false);
        }

        protected override async Task GetImagesMetadataAsync(string apiUrl)
        {
            // to store the IDs of each project
            var tasks = new List<Task>();
            var rawResponse = await Client.GetStringAsync(apiUrl).ConfigureAwait(false);
            var responseJson = JObject.Parse(rawResponse);
            if (!IsLoggedIn)
            {
                tasks.AddRange((responseJson["body"]["user_illust_ids"] ?? throw new InvalidOperationException())
                    .Select(illust_id => Task.Run(async () =>
                    {
                        await GetImageURLsWithoutLoginAsync(string.Format(IllustProjectUrl, illust_id))
                            .ConfigureAwait(false);
                    })));
            }
            else
            {
                while (true)
                {
                    tasks.Add(
                        Task.Run(() =>
                        {
                            GetImageURLsWithLogin(responseJson);
                        })
                    );
                    if (string.IsNullOrEmpty(responseJson["next_url"].ToString())) break;
                    rawResponse = await Client.GetStringAsync(responseJson["next_url"].ToString()).ConfigureAwait(false);
                    responseJson = JObject.Parse(rawResponse);
                }
            }
            var t = Task.WhenAll(tasks.ToArray());
            try
            { await t.ConfigureAwait(false); }
            catch (Exception e)
            {
                OnDownloadStateChanged(new DownloadStateChangedEventArgs(State.DownloadCanceled, e.Message));
            }
        }

        private async Task GetImageURLsWithoutLoginAsync(string illustProject)
        {
            var response = await Client.GetStringAsync(illustProject).ConfigureAwait(false);
            var illustDetails = JObject.Parse(response)["body"]["illust_details"];
            if (Int32.Parse(illustDetails["page_count"].ToString()) > 1)
            {
                foreach (var img_url in illustDetails["manga_a"])
                {
                    lock (ImagesToDownload)
                    {
                        ImagesToDownload.Add(new ImageModel()
                        {
                            Url = img_url["url_big"].ToString(),
                            Name = illustDetails["title"].ToString(),
                            ID = img_url["url_big"].ToString().Split('/').Last().Split('.')[0],
                            FileType = img_url["url_big"].ToString().Split('/').Last().Split('.')[1]
                        });
                    }
                }
            }
            else
            {
                lock (ImagesToDownload)
                {
                    ImagesToDownload.Add(new ImageModel()
                    {
                        Url = illustDetails["url_big"].ToString(),
                        Name = illustDetails["title"].ToString(),
                        ID = illustDetails["id"].ToString(),
                        FileType = illustDetails["url_big"].ToString().Split('/').Last().Split('.')[1]
                    });
                }
            }
        }

        private void GetImageURLsWithLogin(JObject responseJson)
        {
            foreach (var IllustDetails in responseJson["illusts"])
            {
                if (Int32.Parse(IllustDetails["page_count"].ToString()) > 1)
                {
                    foreach (var img_url in IllustDetails["meta_pages"])
                    {
                        lock (ImagesToDownload)
                        {
                            ImagesToDownload.Add(new ImageModel()
                            {
                                Url = img_url["image_urls"]["original"].ToString(),
                                Name = IllustDetails["title"].ToString(),
                                ID = img_url["image_urls"]["original"].ToString().Split('/').Last().Split('.')[0],
                                FileType = img_url["image_urls"]["original"].ToString().Split('/').Last().Split('.')[1]
                            });
                        }

                    }
                }
                else
                {
                    lock (ImagesToDownload)
                    {
                        ImagesToDownload.Add(new ImageModel()
                        {
                            Url = IllustDetails["meta_single_page"]["original_image_url"].ToString(),
                            Name = IllustDetails["title"].ToString(),
                            ID = IllustDetails["id"].ToString(),
                            FileType = IllustDetails["meta_single_page"]["original_image_url"].ToString().Split('/').Last().Split('.')[1]
                        });
                    }

                }
            }
        }

        public override async Task<bool> auth(string refreshToken)
        {
            if (IsLoggedIn) return true;
            var clientTime = DateTime.UtcNow.ToString("s") + "+00:00";
            var data = new Dictionary<string, string>()
            {
                {"client_id", "KzEZED7aC0vird8jWyHM38mXjNTY" },
                {"client_secret", "W9JZoJe00qPvJsiyCGT3CCtC6ZUtdpKpzMbNlUGP" },
                {"get_secure_url", "1" },
                {"grant_type", "refresh_token" },
                {"refresh_token", refreshToken },
            };
            Client.DefaultRequestHeaders.Add("X-Client-Time", clientTime);
            Client.DefaultRequestHeaders.Add("X-Client-Hash", General.CreateMD5(clientTime + LOGIN_SECRET));
            using var content = new FormUrlEncodedContent(data);
            try
            {
                var response = await Client.PostAsync(AUTH_URL, content).ConfigureAwait(false);
                var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                if (jsonResponse.ContainsKey("has_error"))
                {
                    return false;
                }
                var accessToken = jsonResponse["response"]["access_token"]?.ToString() ??
                                  throw new Exception("Bad API Response");
                RefreshToken = jsonResponse["response"]["refresh_token"].ToString();
                if (Client.DefaultRequestHeaders.Contains("Authorization"))
                    Client.DefaultRequestHeaders.Remove("Authorization");
                Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
            }
            catch (HttpRequestException)
            {
                return false;
            }
            return IsLoggedIn = true;
        }

        public override async Task<string> login(string username, string password)
        {
            var clientTime = DateTime.UtcNow.ToString("s") + "+00:00";
            var data = new Dictionary<string, string>()
            {
                {"client_id", "KzEZED7aC0vird8jWyHM38mXjNTY" },
                {"client_secret", "W9JZoJe00qPvJsiyCGT3CCtC6ZUtdpKpzMbNlUGP" },
                {"get_secure_url", "1" },
                {"grant_type", "password" },
                {"username", username },
                {"password", password }
            };
            Client.DefaultRequestHeaders.Add("X-Client-Time", clientTime);
            Client.DefaultRequestHeaders.Add("X-Client-Hash", General.CreateMD5(clientTime + LOGIN_SECRET));
            using var content = new FormUrlEncodedContent(data);
            try
            {
                var response = await Client.PostAsync(AUTH_URL, content).ConfigureAwait(false);
                var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                if (jsonResponse.ContainsKey("has_error"))
                {
                    return null;
                }
                var accessToken = jsonResponse["response"]["access_token"]?.ToString() ??
                                  throw new Exception("Bad API Response");
                RefreshToken = jsonResponse["response"]["refresh_token"].ToString();
                if (Client.DefaultRequestHeaders.Contains("Authorization"))
                    Client.DefaultRequestHeaders.Remove("Authorization");
                Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
            }
            catch (HttpRequestException)
            {
                return null;
            }
            IsLoggedIn = true;
            return RefreshToken;
        }
    }
}
