using DentalRay.Api.Models;

namespace DentalRay.Api.Services.Pos
{
    /// <summary>
    /// Resolves the protocol implementation named in the POS settings.
    /// Registering a new protocol here is all that is needed to support a new
    /// vendor; the controller and the settings screen pick it up automatically.
    /// </summary>
    public sealed class PosProtocolRegistry
    {
        private readonly Dictionary<string, IPosProtocol> _protocols;

        public PosProtocolRegistry(IEnumerable<IPosProtocol> protocols)
        {
            _protocols = protocols.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        }

        public IPosProtocol Resolve(string? name)
        {
            if (!string.IsNullOrWhiteSpace(name) && _protocols.TryGetValue(name, out var found))
                return found;
            // Fall back to Generic so a typo in the settings never breaks payments.
            return _protocols.TryGetValue("Generic", out var generic)
                ? generic
                : throw new InvalidOperationException("No POS protocol is registered.");
        }

        public IReadOnlyCollection<(string Name, string Description)> Available =>
            _protocols.Values.Select(p => (p.Name, p.Description)).ToArray();
    }
}
