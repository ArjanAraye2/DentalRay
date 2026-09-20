using System.Text;

namespace DentalRay.Api.Services.Pos
{
    /// <summary>
    /// Talks to a POS terminal over TCP using a request pattern the administrator
    /// configures in the settings screen.
    ///
    /// The pattern supports these placeholders:
    ///   {amount}    the amount, formatted with two decimals
    ///   {amountRaw} the amount as a plain number without separators
    ///   {invoice}   the payment identifier
    ///   {newline}   a line break
    ///   {cr}        carriage return
    ///
    /// The response is read until the terminal closes the connection or the
    /// pattern below stops matching, and success is decided by SuccessPattern
    /// (an empty SuccessPattern means "any answer counts as success").
    /// </summary>
    public sealed class GenericTcpPosProtocol : IPosProtocol
    {
        public string Name => "Generic";
        public string Description => "ارسال متن قابل تنظیم روی TCP/IP به پورت پوز";

        private readonly ILogger<GenericTcpPosProtocol> _logger;
        public GenericTcpPosProtocol(ILogger<GenericTcpPosProtocol> logger) => _logger = logger;

        public async Task<PosResult> SendAmountAsync(PosRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Host))
                return PosResult.Fail("آدرس IP پوز تنظیم نشده است.");
            if (request.Port is < 1 or > 65535)
                return PosResult.Fail("پورت پوز معتبر نیست.");

            string payload = BuildPayload(request);
            Encoding encoding = ResolveEncoding(request.Encoding);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds, 1, 60)));

            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                await client.ConnectAsync(request.Host, request.Port, timeout.Token);

                await using var stream = client.GetStream();
                byte[] bytes = encoding.GetBytes(payload);
                await stream.WriteAsync(bytes, timeout.Token);
                await stream.FlushAsync(timeout.Token);

                // Read whatever the terminal answers. Many devices simply echo a
                // result line and keep the socket open, so a short read window is
                // used instead of waiting for a close.
                string response = await ReadResponseAsync(stream, encoding, timeout.Token);
                _logger.LogInformation("POS response from {Host}:{Port}: {Response}",
                    request.Host, request.Port, response);

                if (string.IsNullOrWhiteSpace(request.SuccessPattern))
                    return PosResult.Ok("پاسخ از پوز دریافت شد.", response);

                bool success = response.Contains(request.SuccessPattern, StringComparison.OrdinalIgnoreCase);
                return success
                    ? PosResult.Ok("پوز عملیات را تأیید کرد.", response)
                    : PosResult.Fail("پوز عملیات را تأیید نکرد.", response);
            }
            catch (OperationCanceledException)
            {
                return PosResult.Fail("پاسخی از پوز دریافت نشد (مهلت تمام شد).");
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                _logger.LogWarning(ex, "POS connection failed for {Host}:{Port}", request.Host, request.Port);
                return PosResult.Fail("اتصال به پوز برقرار نشد. آدرس و شبکه را بررسی کنید.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected POS error for {Host}:{Port}", request.Host, request.Port);
                return PosResult.Fail("ارتباط با پوز با خطا مواجه شد.");
            }
        }

        private static string BuildPayload(PosRequest request)
        {
            string pattern = string.IsNullOrWhiteSpace(request.RequestPattern)
                ? "SALE|{amount}|{invoice}{newline}"
                : request.RequestPattern;

            return pattern
                .Replace("{amount}", request.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{amountRaw}", request.Amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{invoice}", request.Invoice ?? string.Empty)
                .Replace("{newline}", "\n")
                .Replace("{cr}", "\r");
        }

        private static Encoding ResolveEncoding(string? name) =>
            string.Equals(name, "ASCII", StringComparison.OrdinalIgnoreCase) ? Encoding.ASCII : Encoding.UTF8;

        private static async Task<string> ReadResponseAsync(Stream stream, Encoding encoding, CancellationToken token)
        {
            var buffer = new byte[1024];
            using var ms = new MemoryStream();
            try
            {
                while (ms.Length < 4096)
                {
                    int read = await stream.ReadAsync(buffer, token);
                    if (read <= 0) break;
                    ms.Write(buffer, 0, read);
                    // Most terminals answer with a single short line.
                    if (Array.IndexOf(buffer, (byte)'\n', 0, read) >= 0) break;
                }
            }
            catch (OperationCanceledException) { /* the read window elapsed; keep what arrived */ }
            return encoding.GetString(ms.ToArray()).Trim();
        }
    }
}
