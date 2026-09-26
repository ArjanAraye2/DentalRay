using System.Net;
using System.Net.Sockets;

namespace DentalRay.Api.Services
{
    // Absolute addresses handed to a phone.
    //
    // When Dentix is opened on the server itself, Request.Host is "localhost" -
    // a phone cannot reach that, so the QR/link would be useless. The order is:
    // the configured public host, then the machine's LAN address, and only then
    // the address the request came from (which is fine when the clinic already
    // browses by IP or domain).
    public static class PublicUrl
    {
        public static string BaseUrl(HttpContext context, IConfiguration configuration)
        {
            string? publicHost = configuration["RemoteAccess:PublicHost"]?.Trim();
            if (!string.IsNullOrWhiteSpace(publicHost))
            {
                string scheme = configuration["RemoteAccess:PublicScheme"]?.Trim() ?? "http";
                int port = configuration.GetValue<int?>("RemoteAccess:PublicPort") ?? 5202;
                bool defaultPort = (scheme == "http" && port == 80) || (scheme == "https" && port == 443);
                return $"{scheme}://{publicHost}{(defaultPort ? "" : $":{port}")}";
            }

            string host = context.Request.Host.Host;
            if (host is "localhost" or "127.0.0.1" or "::1")
            {
                try
                {
                    string lan = Dns.GetHostAddresses(Dns.GetHostName())
                        .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                        ?.ToString() ?? string.Empty;
                    int port = context.Request.Host.Port ?? 5202;
                    if (!string.IsNullOrEmpty(lan)) return $"{context.Request.Scheme}://{lan}:{port}";
                }
                catch { /* falls through to the request host */ }
            }

            return $"{context.Request.Scheme}://{context.Request.Host}";
        }
    }
}
