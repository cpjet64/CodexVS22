using System;
using global::CodexVS22;

namespace CodexVS22.Shared.Cli
{
    public interface ICodexOptionsProvider
    {
        CodexOptions GetCurrentOptions();

        event EventHandler OptionsChanged;
    }
}
