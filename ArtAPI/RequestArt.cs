using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ArtAPI.misc;
using DownloadProgressChangedEventArgs = ArtAPI.misc.DownloadProgressChangedEventArgs;

namespace ArtAPI
{
    public abstract class RequestArt
    {
        #region private fields
        private int
            _clientTimeout = 20,
            _downloadAttempts = 10,
            _concurrentTasks = 15;
        private string _artistDirSavepath;
        private int _progress;
        private readonly TimeoutHandler _handler;
        private CancellationTokenSource _cts;
        #endregion
        #region protected fields
        protected HttpClient Client { get; }
        protected virtual string Header { get; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3809.132 Safari/537.36";
        protected List<ImageModel> ImagesToDownload { get; } = new List<ImageModel>();
        #endregion
        #region public properties

        public int ClientTimeout
        {
            get => _clientTimeout;
            set => _clientTimeout = value > 0 ? value : 1;
        }
        public int DownloadAttempts
        {
            get => _downloadAttempts;
            set => _downloadAttempts = value > 0 ? value : 1;
        }
        public int ConcurrentTasks
        {
            get => _concurrentTasks;
            set => _concurrentTasks = value > 0 ? value : 1;
        }
        public State CurrentState { get; protected set; }
        public LoginStatus LoginState { get; protected set; }
        /// <summary>
        /// path to where the images should be saved
        /// </summary>
        public string SavePath { get; set; }

        public bool IsLoggedIn { get; protected set; }
        /// <summary>
        /// safely increment the progress and notify about the change
        /// </summary>
        public virtual int Progress
        {
            get => _progress;
            // ReSharper disable once ValueParameterNotUsed
            protected set
            {
                Interlocked.Increment(ref _progress);
                OnDownloadProgressChanged(new DownloadProgressChangedEventArgs(_progress));
            }
        }
        /// <summary>
        /// the total number of images to download
        /// </summary>
        public virtual int TotalImageCount => ImagesToDownload.Count;

        public int FailedDownloads => TotalImageCount - Progress;

        #endregion

        #region ctor & dtor
        protected RequestArt()
        {
            _handler = new TimeoutHandler()
            {
                InnerHandler = new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                }
            };
            Client = new HttpClient(_handler);
            Client.DefaultRequestHeaders.Add("User-Agent", Header);
            Client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            Client.Timeout = Timeout.InfiniteTimeSpan;
        }
        ~RequestArt()
        {
            Client?.Dispose();
            _handler?.Dispose();
        }
        #endregion
        #region Event handler
        public event EventHandler<DownloadStateChangedEventArgs> DownloadStateChanged;
        public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;
        public event EventHandler<LoginStatusChangedEventArgs> LoginStatusChanged;
        protected void OnDownloadProgressChanged(DownloadProgressChangedEventArgs e)
        {
            DownloadProgressChanged?.Invoke(this, e);
        }
        protected void OnDownloadStateChanged(DownloadStateChangedEventArgs e)
        {
            DownloadStateChanged?.Invoke(this, e);
            if (e.state is { })
            {
                CurrentState = e.state;
            }
        }
        protected void OnLoginStatusChanged(LoginStatusChangedEventArgs e)
        {
            LoginStatusChanged?.Invoke(this, e);
            LoginState = e.Status;
        }
        #endregion

        /// <summary>
        /// downloads all images from a list asynchronous,
        /// should be called only after the <see cref="ImagesToDownload"/> has been populated
        /// </summary>
        protected async Task DownloadImagesAsync()
        {
            if (SavePath == null) throw new Exception("save path not set");
            if (TotalImageCount == 0)
            {
                OnDownloadStateChanged(new DownloadStateChangedEventArgs(State.DownloadCanceled, "No images to download"));
                Clear(); return;
            }
            CheckLocalImages();
            OnDownloadStateChanged(new DownloadStateChangedEventArgs(State.DownloadRunning, TotalImageCount: TotalImageCount));
            _cts = new CancellationTokenSource();
            var ss = new SemaphoreSlim(_concurrentTasks);
            var t = Task.WhenAll(ImagesToDownload.Select(image => Task.Run(async () =>
            {
                // ReSharper disable once AccessToDisposedClosure
                await ss.WaitAsync();
                await DownloadAsync(image, _artistDirSavepath).ConfigureAwait(false);
                // ReSharper disable once AccessToDisposedClosure
                ss.Release();
            })).ToArray());
            try
            {
                await t.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                OnDownloadStateChanged(
                    new DownloadStateChangedEventArgs(State.ExceptionRaised, e.Message, FailedDownloads));
            }
            finally
            {
                OnDownloadStateChanged(!_cts.IsCancellationRequested
                    ? new DownloadStateChangedEventArgs(State.DownloadCompleted, FailedDownloads: FailedDownloads)
                    : new DownloadStateChangedEventArgs(State.DownloadCanceled, FailedDownloads: FailedDownloads));
                ss.Dispose();
                Clear();
            }
        }
        /// <summary>
        /// downloads one image asynchronous
        /// </summary>
        /// <param name="image"><see cref="ImageModel"/> object which contains image url</param>
        /// <param name="savePath">path where the image will be downloaded to</param>
        protected async Task DownloadAsync(ImageModel image, string savePath)
        {
            if (image == null) return;
            var imageName = General.NormalizeFileName(image.Name);
            var imageSavePath = Path.Combine(savePath, $"{imageName}_{image.ID}.{image.FileType}");
            try
            {
                await TryDownloadAsync(image.Url, imageSavePath);
            }
            catch (TimeoutException)
            {
                OnDownloadStateChanged(new DownloadStateChangedEventArgs(State.ExceptionRaised, "Timeout"));
            }
            catch (Exception e)
            { // notify about the exception
                OnDownloadStateChanged(new DownloadStateChangedEventArgs(State.ExceptionRaised, e.Message));
            }
        }

        private async Task TryDownloadAsync(string imgUrl, string imageSavePath)
        {
            for (var i = _downloadAttempts; i > 0; i--)
            {   // if download fails, try to download again
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, imgUrl);
                    request.SetTimeout(new TimeSpan(0, 0, ClientTimeout)); // set the timeout for every request separately
                    using (var asyncResponse = await Client.SendAsync(request, _cts.Token).ConfigureAwait(false))
                    {
                        if (asyncResponse.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            // TODO: fix this later; handle this error somehow, like try to auth again
                            i = 1;
                            throw new Exception("API token expired");
                        }
                        await using var fstream = new FileStream(imageSavePath, FileMode.Create);
                        await (await asyncResponse.Content.ReadAsStreamAsync()).CopyToAsync(fstream)
                            .ConfigureAwait(false);
                    }
                    Progress++;
                    return;
                }
                catch (Exception)
                {
                    if (i == 1 || _cts.IsCancellationRequested) throw;
                    // if there's a some timeout or connection error, wait random amount of time before trying again
                    await Task.Delay(new Random().Next(500, 3000)).ConfigureAwait(false);
                }
            }
        }
        /// <summary>
        /// create a directory for the images
        /// </summary>
        /// <param name="artistName">name of the artist</param>
        protected void CreateSaveDir(string artistName)
        {
            _artistDirSavepath = Path.Combine(SavePath, artistName);
            Directory.CreateDirectory(_artistDirSavepath);
        }
        private void Clear()
        {
            _progress = 0;
            _cts?.Dispose();
            ImagesToDownload.Clear();
        }
        /// <summary>
        /// checks if there are already pictures saved locally and removes them from the list <see cref="ImagesToDownload"/>
        /// </summary>
        private void CheckLocalImages()
        {
            var localImages = Directory.GetFiles(_artistDirSavepath).Select(Path.GetFileNameWithoutExtension).ToArray();
            ImagesToDownload.RemoveAll(image => localImages.Contains($"{General.NormalizeFileName(image.Name)}_{image.ID}"));
        }

        public void CancelDownload()
        {
            try
            {
                _cts?.Cancel();
                OnDownloadStateChanged(new DownloadStateChangedEventArgs(State.DownloadCanceled,
                    FailedDownloads: FailedDownloads));
            }
            catch (ObjectDisposedException)
            { }
        }
        /// <summary>
        /// public method which will be used to download images
        /// </summary>
        /// <param name="artistUrl">artist profile url</param>
        public virtual async Task GetImagesAsync(string artistUrl)
        {
            await GetImagesAsync(new Uri(artistUrl)).ConfigureAwait(false);
        }
        /// <summary>
        /// public method which will be used to download images
        /// </summary>
        /// <param name="artistUrl">Uri object with artist profile url</param>
        public abstract Task GetImagesAsync(Uri artistUrl);

        /// <summary>
        /// creates API-URL from given username
        /// </summary>
        /// <param name="artistName">user name of an artist</param>
        /// <returns>URL with which an API request can be created</returns>
        public abstract Task<Uri> CreateUrlFromName(string artistName);
        public abstract Task<bool> CheckArtistExistsAsync(string artistName);
        protected abstract Task GetImagesMetadataAsync(string apiUrl);
        public virtual Task<bool> Auth(string refreshToken) => Task.FromResult(true);
        public virtual Task<string> Login(string username, string password) => Task.FromResult("");
    }
}
