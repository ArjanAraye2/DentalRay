using System.Net;

namespace DentalRay.Api.Services
{
    // The share-link fetcher calls back into this same server
    // (http://localhost:...). When a VPN/proxy is configured system wide, those
    // loopback calls go through it and come back as a failure, so the pictures
    // look "missing" even though the link answers perfectly in a browser.
    //
    // Loopback therefore always connects directly, while every other address
    // keeps the system proxy - a clinic that needs one still gets it.
    public sealed class LoopbackBypassProxy : IWebProxy
    {
        public ICredentials? Credentials { get; set; }

        public bool IsBypassed(Uri? host) =>
            host != null && (host.IsLoopback || host.Host is "localhost" or "127.0.0.1");

        public Uri? GetProxy(Uri destination) =>
            HttpClient.DefaultProxy?.GetProxy(destination);
    }
}
