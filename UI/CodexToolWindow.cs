using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace CodexVs {
  [Guid("5f8514c1-913c-46bb-b8de-f9fa1a48f42c")]
  public class CodexToolWindow : ToolWindowPane {
    public CodexToolWindow() : base(null) {
      this.Caption = "Codex";
      this.Content = new UI.CodexToolWindowControl();
    }
  }
}