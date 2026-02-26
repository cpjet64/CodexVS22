using System.Threading;
using System.Threading.Tasks;

namespace CodexVS22.Shared.Cli
{
    public interface ICliDiagnosticsSink
    {
        Task LogAsync(CliDiagnostic diagnostic, CancellationToken cancellationToken);
    }
}
