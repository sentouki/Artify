using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ArtAPI.misc
{
    /// <summary>
    /// Better timeout handling with HttpClient with the ability to specify timeout on a per-request basis
    /// code pieces taken from https://thomaslevesque.com/2018/02/25/better-timeout-handling-with-httpclient/
    /// </summary>

    public static class HttpRequestExtensions
    {
        private const string TimeoutPropertyKey = "RequestTimeout";

        public static void SetTimeout(this HttpRequestMessage request, TimeSpan? timeout)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.Properties[TimeoutPropertyKey] = timeout;
        }
        public static TimeSpan? GetTimeout(this HttpRequestMessage request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Properties.TryGetValue(TimeoutPropertyKey, out var value) && value is TimeSpan timeout)
                return timeout;
            return null;
        }

        /// <summary>
        /// modified version of the <see cref="HttpClient.GetStringAsync"/> method which allows to specify timeout on a per-request basis
        /// </summary>
        /// <param name="client"><see cref="HttpClient"/> object</param>
        /// <param name="uri">request uri</param>
        /// <param name="timeout_s">request timeout in seconds</param>
        /// <returns></returns>
        public static async Task<string> GetStringAsyncM(this HttpClient client, string uri, int timeout_s = 150)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.SetTimeout(new TimeSpan(0, 0, timeout_s));
            var response = await client.SendAsync(request).ConfigureAwait(false);
            return await response.Content.ReadAsStringAsync();
        }
    }

    public class TimeoutHandler : DelegatingHandler
    {
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(150);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var cts = GetCancellationTokenSource(request, cancellationToken);
            try
            {
                return await base.SendAsync(request, cts?.Token ?? cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException();
            }
        }
        private CancellationTokenSource GetCancellationTokenSource(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var timeout = request.GetTimeout() ?? DefaultTimeout;
            if (timeout == Timeout.InfiniteTimeSpan) return null;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            return cts;
        }
    }
}
