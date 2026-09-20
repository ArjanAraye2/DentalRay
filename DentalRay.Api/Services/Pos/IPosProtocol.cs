namespace DentalRay.Api.Services.Pos
{
    /// <summary>Outcome of talking to a POS terminal.</summary>
    public sealed record PosResult(bool Success, string Message, string? RawResponse = null)
    {
        public static PosResult Ok(string message, string? raw = null) => new(true, message, raw);
        public static PosResult Fail(string message, string? raw = null) => new(false, message, raw);
    }

    /// <summary>
    /// A way of talking to a POS terminal.
    ///
    /// Dentix ships a Generic implementation driven by the settings stored in
    /// tblPosSettings. A vendor with a real protocol (PAX, Sunmi, a bank SDK,
    /// ...) can be added by implementing this interface and registering it; the
    /// rest of the application does not change.
    /// </summary>
    public interface IPosProtocol
    {
        /// <summary>Name shown in the settings screen.</summary>
        string Name { get; }

        /// <summary>Human readable description of what the protocol expects.</summary>
        string Description { get; }

        /// <summary>Sends an amount to the terminal and reports what happened.</summary>
        Task<PosResult> SendAmountAsync(PosRequest request, CancellationToken cancellationToken);
    }

    /// <summary>Everything a protocol needs in order to contact a terminal.</summary>
    public sealed class PosRequest
    {
        public required string Host { get; init; }
        public required int Port { get; init; }
        public required decimal Amount { get; init; }
        public string? Invoice { get; init; }
        public string? RequestPattern { get; init; }
        public string? SuccessPattern { get; init; }
        public string Encoding { get; init; } = "UTF8";
        public int TimeoutSeconds { get; init; } = 5;
    }
}
