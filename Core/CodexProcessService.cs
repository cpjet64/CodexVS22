using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace CodexVs.Core {
  public sealed class CodexProcessService {
    private static readonly Lazy<CodexProcessService> _lazy =
      new(() => new CodexProcessService());
    public static CodexProcessService Instance => _lazy.Value;
    private Process _proc;
    private StreamWriter _stdin;
    private CancellationTokenSource _cts;
    public event Action<string> OnMessage;
    private CodexProcessService() { }
    public async Task<bool> TryStartAsync() {
      if (_proc != null && !_proc.HasExited) return true;
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      var startInfo = new ProcessStartInfo {
        FileName = "codex",
        Arguments = "proto",
        UseShellExecute = false,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
        WorkingDirectory = Environment.CurrentDirectory
      };
      _cts = new CancellationTokenSource();
      try {
        _proc = Process.Start(startInfo);
        _stdin = _proc.StandardInput;
      } catch (Exception ex) {
        OnMessage?.Invoke($"{{\"level\":\"error\",\"msg\":\"start: {ex.Message}\"}}");
        return false;
      }
      _ = Task.Run(() => PumpAsync(_proc.StandardOutput, _cts.Token));
      _ = Task.Run(() => PumpAsync(_proc.StandardError, _cts.Token));
      OnMessage?.Invoke("{\"level\":\"info\",\"msg\":\"codex started\"}");
      return true;
    }
    public async Task SendAsync(string jsonLine) {
      try {
        if (_stdin == null) return;
        await _stdin.WriteLineAsync(jsonLine);
        await _stdin.FlushAsync();
      } catch (Exception ex) {
        OnMessage?.Invoke($"{{\"level\":\"error\",\"msg\":\"write: {ex.Message}\"}}");
      }
    }
    private async Task PumpAsync(StreamReader reader, CancellationToken token) {
      string line;
      try {
        while (!token.IsCancellationRequested &&
          (line = await reader.ReadLineAsync()) != null) {
          OnMessage?.Invoke(line);
        }
      } catch (Exception ex) {
        OnMessage?.Invoke($"{{\"level\":\"error\",\"msg\":\"pump: {ex.Message}\"}}");
      }
    }
  }
}