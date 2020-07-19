using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ArtAPI
{
    public interface IRequestArt
    {
        #region Events
        /// <summary>
        /// notify the GUI/CLI about states current state
        /// </summary>
        event EventHandler<DownloadStateChangedEventArgs> DownloadStateChanged;
        /// <summary>
        /// notify the GUI/CLI about download progress
        /// </summary>
        event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;
        #endregion
        string SavePath { get; set; }
        /// <summary>
        /// public method which will be used to download images
        /// </summary>
        /// <param name="artistUrl">artist profile url</param>
        Task GetImagesAsync(string artistUrl);
        /// <summary>
        /// public method which will be used to download images
        /// </summary>
        /// <param name="artistUrl">Uri object with artist profile url</param>
        Task GetImagesAsync(Uri artistUrl);
        /// <summary>
        /// creates API-URL from given username
        /// </summary>
        /// <param name="artistName">user name of an artist</param>
        /// <returns>URL with which an API request can be created</returns>
        Uri CreateUrlFromName(string artistName);
        void CancelDownload();
        Task<bool> CheckArtistExistsAsync(string artistName);
        Task<bool> auth(string refreshToken);
        Task<string> login(string username, string password);
    }

    public abstract class RequestArt : IRequestArt
    {
        private const int
            ClientTimeout = 20,
            NumberOfDLAttempts = 10,
            ConcurrentTasks = 15;
        private string ArtistDirSavepath;
        private int _progress;
        public State? CurrentState { get; protected set; }
        protected HttpClient Client { get; }
        protected HttpClientHandler handler { get; }
        private CancellationTokenSource cts;
        protected virtual string Header { get; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3809.132 Safari/537.36";
        protected List<ImageModel> ImagesToDownload { get; } = new List<ImageModel>();

        #region ctor & dtor
        protected RequestArt()
        {
            handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            Client = new HttpClient(handler);
            Client.DefaultRequestHeaders.Add("User-Agent", Header);
            Client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            Client.Timeout = new TimeSpan(0, 0, ClientTimeout);
        }
        ~RequestArt()
        {

            Client?.Dispose();
            handler?.Dispose();
        }
        #endregion
        #region properties
        /// <summary>
        /// path to where the images should be saved
        /// </summary>
        public string SavePath { get; set; }
        /// <summary>
        /// safely increment the progress and notify about the change
        /// </summary>
        public virtual int Progress
        {
            get => _progress;
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
        #region Event handler
        public event EventHandler<DownloadStateChangedEventArgs> DownloadStateChanged;
        public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;
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
            cts = new CancellationTokenSource();
            using var ss = new SemaphoreSlim(ConcurrentTasks);
            var tasks = new List<Task>();
            foreach (var image in ImagesToDownload)
            {
                await ss.WaitAsync();
                tasks.Add(
                    Task.Run(async () =>
                        {
                            await DownloadAsync(image, ArtistDirSavepath).ConfigureAwait(false);
                            ss.Release();
                        })
                );
            }
            var t = Task.WhenAll(tasks.ToArray());
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
                OnDownloadStateChanged(!cts.IsCancellationRequested
                    ? new DownloadStateChangedEventArgs(State.DownloadCompleted, FailedDownloads: FailedDownloads)
                    : new DownloadStateChangedEventArgs(State.DownloadCanceled, FailedDownloads: FailedDownloads));
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
            var imageName = NormalizeFileName(image.Name);
            var imageSavePath = Path.Combine(savePath, $"{imageName}_{image.ID}.{image.FileType}");
            const int tries = NumberOfDLAttempts;
            try
            {
                for (var i = tries; i > 0; i--)
                {   // if download fails, try to download again
                    try
                    {
                        using (var asyncResponse = await Client.GetAsync(image.Url, cts.Token).ConfigureAwait(false))
                        await using (var fstream = new FileStream(imageSavePath, FileMode.Create))
                        {
                            await (await asyncResponse.Content.ReadAsStreamAsync()).CopyToAsync(fstream).ConfigureAwait(false);
                        }
                        Progress++;
                        return;
                    }
                    catch (Exception)
                    {
                        if (i == 1 || cts.IsCancellationRequested) throw;
                        // if there's a some timeout or connection error, wait random amount of time before trying again
                        await Task.Delay(new Random().Next(500, 2000)).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception e)
            { // notify about the exception
                OnDownloadStateChanged(new DownloadStateChangedEventArgs(State.ExceptionRaised, e.Message));
            }
        }
        /// <summary>
        /// create a directory for the images
        /// </summary>
        /// <param name="artistName">name of the artist</param>
        protected void CreateSaveDir(string artistName)
        {
            ArtistDirSavepath = Path.Combine(SavePath, artistName);
            Directory.CreateDirectory(ArtistDirSavepath);
        }
        private void Clear()
        {
            _progress = 0;
            CurrentState = null;
            cts?.Dispose();
            ImagesToDownload.Clear();
        }
        /// <summary>
        /// checks if there are already pictures saved locally and removes them from the list <see cref="ImagesToDownload"/>
        /// </summary>
        private void CheckLocalImages()
        {
            var localImages = Directory.GetFiles(ArtistDirSavepath).Select(Path.GetFileNameWithoutExtension).ToArray();
            ImagesToDownload.RemoveAll(image => localImages.Contains($"{NormalizeFileName(image.Name)}_{image.ID}"));
        }
        /// <summary>
        /// remove all the nasty characters that can cause trouble
        /// </summary>
        /// <returns>normalized file name</returns>
        private string NormalizeFileName(string filename)
        {
            var specialChars = new List<string>() { @"\", "/", ":", "*", "?", "\"", "<", ">", "|" };
            specialChars.ForEach(c => filename = filename.Replace(c, ""));
            return filename;
        }

        public void CancelDownload()
        {
            try
            {
                cts?.Cancel();
                OnDownloadStateChanged(new DownloadStateChangedEventArgs(State.DownloadCanceled,
                    FailedDownloads: FailedDownloads));
            }
            catch (ObjectDisposedException)
            { }
        }
        public virtual async Task GetImagesAsync(string artistUrl)
        {
            await GetImagesAsync(new Uri(artistUrl)).ConfigureAwait(false);
        }
        public abstract Task GetImagesAsync(Uri artistUrl);
        public abstract Uri CreateUrlFromName(string artistName);
        public abstract Task<bool> CheckArtistExistsAsync(string artistName);
        protected abstract Task GetImagesMetadataAsync(string apiUrl);
        public virtual Task<bool> auth(string refreshToken) => Task.FromResult(true);
        public virtual Task<string> login(string username, string password) => Task.FromResult("");
    }
}
