using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
#pragma warning disable CA5351

namespace ArtAPI
{
    public static class General
    {
        /// <summary>
        /// for hashing strings, used for Pixiv API authentication 
        /// </summary>
        /// <returns>hashed string</returns>
        public static string CreateMD5(string input)
        {
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
        /// <summary>
        /// special characters which cannot be used for file and directory names
        /// </summary>
        private static readonly List<string> SpecialChars = new List<string>() { @"\", "/", ":", "*", "?", "\"", "<", ">", "|" };
        /// <summary>
        /// remove all the nasty characters that can cause trouble
        /// </summary>
        /// <returns>normalized file name</returns>
        public static string NormalizeFileName(string filename)
        {
            SpecialChars.ForEach(c => filename = filename.Replace(c, ""));
            return filename;
        }

    }
    public enum State
    {
        DownloadPreparing,
        DownloadRunning,
        DownloadCompleted,
        DownloadCanceled,
        ExceptionRaised,
    }
    public enum LoginStatus
    {
        NotLoggedIn,
        LoggingIn,
        Authenticating,
        LoggedIn,
        Failed
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

    public class LoginStatusChangedEventArgs : EventArgs
    {
        public LoginStatus Status { get; }

        public LoginStatusChangedEventArgs(LoginStatus status)
        {
            Status = status;
        }
    }
    #endregion
}
