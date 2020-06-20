using System;
using System.Security.Cryptography;
using System.Text;
#pragma warning disable CA5351

namespace ArtAPI
{
    public enum State
    {
        DownloadPreparing,
        DownloadRunning,
        DownloadCompleted,
        DownloadCanceled,
        ExceptionRaised,
    }

    public static class General
    {
        public static string CreateMD5(string input)
        {
            // Use input string to calculate MD5 hash
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                var sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }

    #region defining EventArgs
    /// <summary>
    /// contains the information about the current state
    /// and additional information like amount of images which couldn't have been downloaded,
    /// as well as ExceptionMsg if an Exception was raised
    /// </summary>
    public class DownloadStateChangedEventArgs : EventArgs
    {
        public State state { get; }
        public int TotalImageCount { get; }
        public string ExceptionMsg { get; }
        public int FailedDownloads { get; }

        public DownloadStateChangedEventArgs(State state, string ExceptionMsg = null, int FailedDownloads = 0, int TotalImageCount = 0)
        {
            this.state = state;
            this.FailedDownloads = FailedDownloads;
            this.ExceptionMsg = ExceptionMsg;
            this.TotalImageCount = TotalImageCount;
        }
    }
    public class DownloadProgressChangedEventArgs : EventArgs
    {
        public int CurrentProgress { get; }

        public DownloadProgressChangedEventArgs(int progress)
        {
            CurrentProgress = progress;
        }
    }
    #endregion
}
