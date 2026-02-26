using System;
using System.Threading.Tasks;
using CodexVS22.Shared.Cli;

namespace CodexVS22.Core.Cli
{
    public sealed class DefaultCliMessageRouter : ICliMessageRouter
    {
        public event EventHandler<CliEnvelope> EnvelopeReceived;

        public Task RouteAsync(CliEnvelope envelope)
        {
            EnvelopeReceived?.Invoke(this, envelope);
            return Task.CompletedTask;
        }
    }
}
