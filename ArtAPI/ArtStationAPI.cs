using System;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ArtAPI
{
    public sealed class ArtStationAPI : RequestArt
    {
        private const string ApiUrl = @"https://artstation.com/users/{0}/projects?page=";
        private const string AssetsUrl = @"https://www.artstation.com/projects/{0}.json";

        public override Uri CreateUrlFromName(string artistName)
        {
            return new Uri(string.Format($@"https://www.artstation.com/{artistName}"));
        }
        public override async Task<bool> CheckArtistExistsAsync(string artistName)
        {
            var response = await Client.GetAsync(CreateUrlFromName(artistName));
            return response.IsSuccessStatusCode;
        }

        public override async Task GetImagesAsync(string artistUrl)
        {
            await GetImagesAsync(new Uri(artistUrl)).ConfigureAwait(false);
        }

        public override async Task GetImagesAsync(Uri artistUrl)
        {
            OnDownloadStateChanged(new DownloadStateChangedEventArgs(State.DownloadPreparing));
            var artistname = artistUrl?.AbsolutePath.Split('/')[1];
            if (artistname == null) return;
            CreateSaveDir(artistname);
            await GetImagesMetadataAsync(string.Format(ApiUrl, artistname)).ConfigureAwait(false);
        }

        protected override async Task GetImagesMetadataAsync(string apiUrl)
        {
            JContainer allPages;
            var settings = new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Concat };
            try
            {
                string rawResponse = await Client.GetStringAsync(apiUrl).ConfigureAwait(false);
                var responseJson = JObject.Parse(rawResponse);
                int totalCount = Int32.Parse(responseJson["total_count"]?.ToString() ?? throw new Exception("Bad API Response"));
                var pages = (totalCount / 50) + 1;

                allPages = (JContainer)responseJson["data"]; // add the first page to the container
                for (int page = pages; page > 1; page--)     // go through the remaining pages and add them to the container
                {
                    rawResponse = await Client.GetStringAsync(apiUrl + $"{page}").ConfigureAwait(false);
                    allPages.Merge((JContainer)JObject.Parse(rawResponse)["data"], settings);
                }
            }
            catch (Exception e)
            { OnDownloadStateChanged(new DownloadStateChangedEventArgs(State.DownloadCanceled, e.Message)); return; }

            // each project can have one or more images (assets)
            var t = Task.WhenAll(allPages.Select(project => Task.Run(async () => { await GetAssets(project["hash_id"].ToString(), project["title"].ToString()).ConfigureAwait(false); })).ToArray());
            try { await t.ConfigureAwait(false); }
            catch (Exception e)
            { OnDownloadStateChanged(new DownloadStateChangedEventArgs(State.DownloadCanceled, e.Message)); return; }
            await DownloadImagesAsync().ConfigureAwait(false);
        }

        // get all the images from a project
        private async Task GetAssets(string hash_id, string name)
        {
            var response = await Client.GetStringAsync(string.Format(AssetsUrl, hash_id)).ConfigureAwait(false);
            var assetsJson = JObject.Parse(response);
            foreach (var image in assetsJson["assets"] as JContainer)
            {
                lock (ImagesToDownload)
                {
                    ImagesToDownload.Add(new ImageModel()
                    {
                        Url = image["image_url"].ToString(),
                        Name = name,
                        ID = image["id"].ToString()
                    });
                }
            }
        }
    }
}
