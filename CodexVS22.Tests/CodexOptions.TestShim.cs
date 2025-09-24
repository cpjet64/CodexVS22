using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CodexVS22
{
  // Test-only shim to validate options logic without VS dependencies.
  public class CodexOptions
  {
    public enum ApprovalMode { Chat, Agent, AgentFullAccess }
    public enum SandboxPolicyMode { Strict, Moderate, Permissive }

    public string CliExecutable { get; set; } = string.Empty;
    public bool UseWsl { get; set; } = false;
    public bool OpenOnStartup { get; set; } = false;
    public ApprovalMode Mode { get; set; } = ApprovalMode.Chat;
    public SandboxPolicyMode SandboxPolicy { get; set; } = SandboxPolicyMode.Moderate;
    public string DefaultModel { get; set; } = "gpt-4.1";
    public string DefaultReasoning { get; set; } = "medium";
    public bool AutoOpenPatchedFiles { get; set; } = true;
    public bool AutoHideExecConsole { get; set; } = false;
    public bool ExecConsoleVisible { get; set; } = true;
    public double ExecConsoleHeight { get; set; } = 180.0;
    public int ExecOutputBufferLimit { get; set; } = 200000;
    public double WindowWidth { get; set; } = 600.0;
    public double WindowHeight { get; set; } = 700.0;
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public string WindowState { get; set; } = "Normal";
    public string LastUsedTool { get; set; } = string.Empty;
    public string LastUsedPrompt { get; set; } = string.Empty;
    public string SolutionCliExecutable { get; set; } = string.Empty;
    public bool? SolutionUseWsl { get; set; } = null;

    public string ExportToJson()
    {
      var obj = new Newtonsoft.Json.Linq.JObject
      {
        [nameof(CliExecutable)] = CliExecutable,
        [nameof(UseWsl)] = UseWsl,
        [nameof(OpenOnStartup)] = OpenOnStartup,
        [nameof(Mode)] = (int)Mode,
        [nameof(SandboxPolicy)] = (int)SandboxPolicy,
        [nameof(DefaultModel)] = DefaultModel,
        [nameof(DefaultReasoning)] = DefaultReasoning,
        [nameof(AutoOpenPatchedFiles)] = AutoOpenPatchedFiles,
        [nameof(AutoHideExecConsole)] = AutoHideExecConsole,
        [nameof(ExecConsoleVisible)] = ExecConsoleVisible,
        [nameof(ExecConsoleHeight)] = ExecConsoleHeight,
        [nameof(ExecOutputBufferLimit)] = ExecOutputBufferLimit,
        [nameof(WindowWidth)] = WindowWidth,
        [nameof(WindowHeight)] = WindowHeight,
        [nameof(WindowLeft)] = WindowLeft,
        [nameof(WindowTop)] = WindowTop,
        [nameof(WindowState)] = WindowState,
        [nameof(LastUsedTool)] = LastUsedTool,
        [nameof(LastUsedPrompt)] = LastUsedPrompt,
        [nameof(SolutionCliExecutable)] = SolutionCliExecutable,
        [nameof(SolutionUseWsl)] = SolutionUseWsl
      };
      return obj.ToString(Newtonsoft.Json.Formatting.Indented);
    }

    public bool ImportFromJson(string json)
    {
      if (string.IsNullOrWhiteSpace(json)) return false;
      var settings = new JsonSerializerSettings
      {
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore
      };
      var imported = JsonConvert.DeserializeObject<CodexOptions>(json, settings);
      if (imported == null) return false;

      CliExecutable = imported.CliExecutable ?? string.Empty;
      UseWsl = imported.UseWsl;
      OpenOnStartup = imported.OpenOnStartup;
      Mode = imported.Mode;
      SandboxPolicy = imported.SandboxPolicy;
      DefaultModel = imported.DefaultModel ?? "gpt-4.1";
      DefaultReasoning = imported.DefaultReasoning ?? "medium";
      AutoOpenPatchedFiles = imported.AutoOpenPatchedFiles;
      AutoHideExecConsole = imported.AutoHideExecConsole;
      ExecConsoleVisible = imported.ExecConsoleVisible;
      ExecConsoleHeight = imported.ExecConsoleHeight;
      ExecOutputBufferLimit = imported.ExecOutputBufferLimit;
      WindowWidth = imported.WindowWidth;
      WindowHeight = imported.WindowHeight;
      WindowLeft = imported.WindowLeft;
      WindowTop = imported.WindowTop;
      WindowState = imported.WindowState ?? "Normal";
      LastUsedTool = imported.LastUsedTool ?? string.Empty;
      LastUsedPrompt = imported.LastUsedPrompt ?? string.Empty;
      SolutionCliExecutable = imported.SolutionCliExecutable ?? string.Empty;
      SolutionUseWsl = imported.SolutionUseWsl;
      return true;
    }

    public string GetEffectiveCliExecutable()
      => !string.IsNullOrEmpty(SolutionCliExecutable) ? SolutionCliExecutable : CliExecutable;

    public bool GetEffectiveUseWsl() => SolutionUseWsl ?? UseWsl;

    // Test-only validation mirroring logic-layer expectations.
    public void ValidateForTests()
    {
      if (string.IsNullOrWhiteSpace(DefaultModel))
        DefaultModel = "gpt-4.1";

      var allowed = new[] { "none", "medium", "high" };
      if (string.IsNullOrWhiteSpace(DefaultReasoning) ||
          Array.IndexOf(allowed, DefaultReasoning.ToLowerInvariant()) < 0)
        DefaultReasoning = "medium";

      if (WindowWidth < 300 || WindowWidth > 2000)
        WindowWidth = Math.Max(300, Math.Min(2000, WindowWidth));
      if (WindowHeight < 200 || WindowHeight > 1500)
        WindowHeight = Math.Max(200, Math.Min(1500, WindowHeight));
      if (ExecConsoleHeight < 50 || ExecConsoleHeight > 800)
        ExecConsoleHeight = Math.Max(50, Math.Min(800, ExecConsoleHeight));
      if (ExecOutputBufferLimit < 0 || ExecOutputBufferLimit > 10000000)
        ExecOutputBufferLimit = Math.Max(0, Math.Min(10000000, ExecOutputBufferLimit));
      if (string.IsNullOrWhiteSpace(WindowState) ||
          Array.IndexOf(new[] { "Normal", "Maximized", "Minimized" }, WindowState) < 0)
        WindowState = "Normal";
    }
  }
}
