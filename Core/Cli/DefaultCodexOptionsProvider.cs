using System;
using CodexVS22.Shared.Cli;
using global::CodexVS22;

namespace CodexVS22.Core.Cli
{
    public sealed class DefaultCodexOptionsProvider : ICodexOptionsProvider
    {
        public event EventHandler OptionsChanged;

        public CodexOptions GetCurrentOptions()
        {
            return CodexVS22Package.OptionsInstance ?? new CodexOptions();
        }

        public void RaiseOptionsChanged()
        {
            OptionsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
