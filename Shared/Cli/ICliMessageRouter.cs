using System;
using System.Threading.Tasks;

namespace CodexVS22.Shared.Cli
{
    public interface ICliMessageRouter
    {
        event EventHandler<CliEnvelope> EnvelopeReceived;

        Task RouteAsync(CliEnvelope envelope);
    }
}
