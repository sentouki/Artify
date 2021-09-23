using ArtAPI;
using Artify.Models;
using CefSharp;
using CefSharp.Handler;

namespace Artify.Views.misc
{
    class CefRequestHandler : RequestHandler
    {
        protected override IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame,
            IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
        {
            if (request.Url.StartsWith("pixiv://") && request.Url.Contains("code"))
            {
                var code = request.Url.Split("=")[1].Split("&")[0];
                _ = ArtifyModel.Instance.PixivLogin(code);
            }

            return null;
        }
    }
}
