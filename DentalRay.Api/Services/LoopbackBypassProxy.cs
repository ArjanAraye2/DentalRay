using System.Net;

namespace DentalRay.Api.Services
{
    // The share-link fetcher calls back into this same server
    // (localhost or the machine's own LAN address). When a VPN/proxy is
    // configured system wide, those calls go through it and come back as a
    // failure, so the pictures look "missing" even though the link answers
    // perfectly in a browser.
    //
    // Loopback and every private range therefore always connect directly,
    // while public addresses keep the system proxy - a clinic that needs one
    // still gets it, and the internet path stays untouched.
    public sealed class LoopbackBypassProxy : IWebProxy
    {
        public ICredentials? Credentials { get; set; }

        public bool IsBypassed(Uri? host)
        {
            if (host == null) return true;
            if (host.IsLoopback || host.Host is "localhost" or "127.0.0.1") return true;

            if (!IPAddress.TryParse(host.Host, out var address)) return false;
            if (IPAddress.IsLoopback(address)) return true;
            if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;

            byte[] bytes = address.GetAddressBytes();
            return bytes[0] switch
            {
                10 => true,                                        // 10.0.0.0/8
                192 when bytes[1] == 168 => true,                  // 192.168.0.0/16
                172 when bytes[1] >= 16 && bytes[1] <= 31 => true, // 172.16.0.0/12
                169 when bytes[1] == 254 => true,                  // link-local
                _ => false
            };
        }

        public Uri? GetProxy(Uri destination) =>
            HttpClient.DefaultProxy?.GetProxy(destination);
    }
}
