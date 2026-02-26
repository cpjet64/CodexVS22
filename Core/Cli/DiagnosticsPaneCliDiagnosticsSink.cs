using System;
using System.Threading;
using System.Threading.Tasks;
using CodexVS22.Shared.Cli;
using global::CodexVS22.Core;

namespace CodexVS22.Core.Cli
{
    public sealed class DiagnosticsPaneCliDiagnosticsSink : ICliDiagnosticsSink
    {
        private static readonly object Gate = new();
        private static int _rateCount;
        private static int _rateSecond;
        private const int MaxPerSecond = 20;

        public async Task LogAsync(CliDiagnostic diagnostic, CancellationToken cancellationToken)
        {
            if (diagnostic == null)
                return;

            if (!ShouldLog())
                return;

            var pane = await DiagnosticsPane.GetAsync().ConfigureAwait(false);
            var prefix = diagnostic.Severity switch
            {
                CliDiagnosticSeverity.Error => "err ",
                CliDiagnosticSeverity.Warning => "warn",
                _ => "info"
            };

            await pane.WriteLineAsync($"[{prefix}] {DateTime.Now:HH:mm:ss} [{diagnostic.Category}] {diagnostic.Message}").ConfigureAwait(false);
        }

        private static bool ShouldLog()
        {
            var now = DateTime.Now;
            lock (Gate)
            {
                if (_rateSecond != now.Second)
                {
                    _rateSecond = now.Second;
                    _rateCount = 0;
                    return true;
                }

                if (_rateCount++ > MaxPerSecond)
                    return false;

                return true;
            }
        }
    }
}
