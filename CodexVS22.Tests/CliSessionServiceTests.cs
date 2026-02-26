using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodexVS22.Core.Cli;
using CodexVS22.Shared.Cli;
using Newtonsoft.Json.Linq;

namespace CodexVS22.Tests
{
  internal static partial class Program
  {
    private static void CliSessionService_ConnectsAndRoutesStdout()
    {
      var host = new FakeCliHost();
      var router = new RecordingRouter();
      var serializer = new CliSubmissionFactory();
      var optionsProvider = new StubOptionsProvider();

      using var service = new CliSessionService(host, router, serializer, optionsProvider);

      var result = service.ConnectAsync(new CodexOptions(), Environment.CurrentDirectory, CancellationToken.None)
        .GetAwaiter().GetResult();

      if (!result.IsSuccess)
        throw new InvalidOperationException("Connection did not succeed");

      host.EnqueueMessage("{\"event\":{\"type\":\"chat_delta\"}}\n");
      router.WaitForEnvelope(TimeSpan.FromSeconds(1));

      if (router.Envelopes.Count != 1)
        throw new InvalidOperationException("Expected routed envelope");

      service.DisconnectAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    private static void CliSessionService_HeartbeatNegotiationSendsTemplate()
    {
      var host = new FakeCliHost();
      var router = new RecordingRouter();
      var serializer = new CliSubmissionFactory();
      var optionsProvider = new StubOptionsProvider();

      using var service = new CliSessionService(host, router, serializer, optionsProvider);
      var result = service.ConnectAsync(new CodexOptions(), Environment.CurrentDirectory, CancellationToken.None)
        .GetAwaiter().GetResult();

      if (!result.IsSuccess)
        throw new InvalidOperationException("Connection did not succeed");

      var handshake = new JObject
      {
        ["session"] = new JObject
        {
          ["heartbeat"] = new JObject
          {
            ["interval_ms"] = 1500,
            ["op"] = new JObject
            {
              ["type"] = "heartbeat"
            }
          }
        }
      };

      host.EnqueueMessage(handshake.ToString());
      router.WaitForEnvelope(TimeSpan.FromSeconds(1));

      service.EnsureHeartbeatAsync(CancellationToken.None).GetAwaiter().GetResult();

      if (host.SentPayloads.Count == 0)
        throw new InvalidOperationException("Expected heartbeat payload to be sent");

      var payload = host.SentPayloads[^1];
      if (!payload.Contains("\"type\":\"heartbeat\"", StringComparison.Ordinal))
        throw new InvalidOperationException("Heartbeat payload missing type");

      service.DisconnectAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    private static void CliSessionService_DiagnosticsAreForwarded()
    {
      var host = new FakeCliHost();
      var router = new RecordingRouter();
      var serializer = new CliSubmissionFactory();
      var optionsProvider = new StubOptionsProvider();

      using var service = new CliSessionService(host, router, serializer, optionsProvider);
      var result = service.ConnectAsync(new CodexOptions(), Environment.CurrentDirectory, CancellationToken.None)
        .GetAwaiter().GetResult();

      if (!result.IsSuccess)
        throw new InvalidOperationException("Connection did not succeed");

      var completion = new TaskCompletionSource<CliDiagnostic>(TaskCreationOptions.RunContinuationsAsynchronously);
      service.DiagnosticReceived += (_, diagnostic) => completion.TrySetResult(diagnostic);

      host.EnqueueDiagnostic(CliDiagnostic.Info("StdErr", "diagnostic-line"));

      var waited = completion.Task.Wait(TimeSpan.FromSeconds(1));
      if (!waited)
        throw new InvalidOperationException("Diagnostic was not forwarded");

      service.DisconnectAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    private sealed class RecordingRouter : ICliMessageRouter
    {
      private readonly List<CliEnvelope> _envelopes = new();
      private readonly TaskCompletionSource<bool> _first = new(TaskCreationOptions.RunContinuationsAsynchronously);

      public IReadOnlyList<CliEnvelope> Envelopes => _envelopes;

      public event EventHandler<CliEnvelope> EnvelopeReceived;

      public Task RouteAsync(CliEnvelope envelope)
      {
        _envelopes.Add(envelope);
        EnvelopeReceived?.Invoke(this, envelope);
        _first.TrySetResult(true);
        return Task.CompletedTask;
      }

      public void WaitForEnvelope(TimeSpan timeout)
      {
        _first.Task.Wait(timeout);
      }
    }

    private sealed class StubOptionsProvider : ICodexOptionsProvider
    {
      public event EventHandler OptionsChanged;

      public CodexOptions GetCurrentOptions()
      {
        return new CodexOptions();
      }
    }

    private sealed class FakeCliHost : ICodexCliHost
    {
      public List<string> SentPayloads { get; } = new();

      public CliHostState State { get; private set; } = CliHostState.Stopped;

      public event EventHandler<CliHostStateChangedEventArgs> StateChanged;

      public event EventHandler<CliEnvelope> EnvelopeReceived;

      public event EventHandler<CliDiagnostic> DiagnosticReceived;

      public Task<CliConnectionResult> ConnectAsync(CliConnectionRequest request, CancellationToken cancellationToken)
      {
        State = CliHostState.Connected;
        StateChanged?.Invoke(this, new CliHostStateChangedEventArgs(State));
        return Task.FromResult(CliConnectionResult.Success);
      }

      public Task DisconnectAsync(CancellationToken cancellationToken)
      {
        State = CliHostState.Stopped;
        StateChanged?.Invoke(this, new CliHostStateChangedEventArgs(State));
        return Task.CompletedTask;
      }

      public Task<bool> SendAsync(string payload, CancellationToken cancellationToken = default)
      {
        SentPayloads.Add(payload);
        return Task.FromResult(true);
      }

      public Task<CodexAuthenticationResult> CheckAuthenticationAsync(CancellationToken cancellationToken)
      {
        return Task.FromResult(new CodexAuthenticationResult(true, string.Empty));
      }

      public Task<bool> LoginAsync(CancellationToken cancellationToken)
      {
        return Task.FromResult(true);
      }

      public Task<bool> LogoutAsync(CancellationToken cancellationToken)
      {
        return Task.FromResult(true);
      }

      public Task<CliHeartbeatInfo> EnsureHeartbeatAsync(CancellationToken cancellationToken)
      {
        return Task.FromResult(CliHeartbeatInfo.Empty);
      }

      public Task<CliHealthSnapshot> GetHealthAsync(CancellationToken cancellationToken)
      {
        return Task.FromResult(new CliHealthSnapshot(0, TimeSpan.Zero, State));
      }

      public void Dispose()
      {
      }

      public void EnqueueMessage(string raw)
      {
        EnvelopeReceived?.Invoke(this, CliEnvelope.FromRaw(raw));
      }

      public void EnqueueDiagnostic(CliDiagnostic diagnostic)
      {
        DiagnosticReceived?.Invoke(this, diagnostic);
      }
    }
  }
}

namespace CodexVS22
{
  public static class CodexVS22Package
  {
    public static CodexOptions OptionsInstance { get; set; } = new CodexOptions();
  }
}

namespace CodexVS22.Core.Cli
{
  public class ProcessCodexCliHost
  {
    public void SetHeartbeatInfo(CodexVS22.Shared.Cli.CliHeartbeatInfo info)
    {
      // no-op stub for unit tests
    }
  }
}
