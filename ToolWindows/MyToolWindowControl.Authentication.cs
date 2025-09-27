using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Community.VisualStudio.Toolkit;
using CodexVS22.Core;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private async Task UpdateAuthenticationStateAsync(
      bool known,
      bool isAuthenticated,
      string message,
      bool inProgress)
    {
      _authKnown = known;
      _isAuthenticated = isAuthenticated;
      _authMessage = message ?? string.Empty;
      _authOperationInProgress = inProgress;
      await RefreshAuthUiAsync();
    }

    private async Task RefreshAuthUiAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

      var banner = this.FindName("AuthBanner") as Border;
      var text = this.FindName("AuthMessage") as TextBlock;
      var login = this.FindName("LoginButton") as Button;
      var logout = this.FindName("LogoutButton") as Button;
      var shouldShowBanner = _authOperationInProgress || (!_isAuthenticated && _authKnown);

      if (banner != null)
        banner.Visibility = shouldShowBanner ? Visibility.Visible : Visibility.Collapsed;

      if (text != null)
      {
        if (!string.IsNullOrWhiteSpace(_authMessage))
          text.Text = _authMessage;
        else if (_authOperationInProgress)
          text.Text = "Checking Codex authentication...";
        else if (_authKnown && !_isAuthenticated)
          text.Text = "Codex login required. Click Login to continue.";
        else
          text.Text = "Codex is authenticated.";
      }

      if (login != null)
      {
        var showLogin = !_isAuthenticated || _authOperationInProgress;
        login.Visibility = showLogin ? Visibility.Visible : Visibility.Collapsed;
        login.IsEnabled = !_authOperationInProgress && !_isAuthenticated;
      }

      if (logout != null)
      {
        logout.Visibility = _authKnown && _isAuthenticated ? Visibility.Visible : Visibility.Collapsed;
        logout.IsEnabled = !_authOperationInProgress;
      }

      if (this.FindName("SendButton") is Button send)
      {
        if (!_authKnown || !_isAuthenticated || _authOperationInProgress)
        {
          if (send.IsEnabled)
          {
            send.IsEnabled = false;
            _authGatedSend = true;
          }
        }
        else if (_authGatedSend)
        {
          send.IsEnabled = true;
          _authGatedSend = false;
        }
      }
    }

    private async Task HandleAuthenticationResultAsync(
      CodexCliHost.CodexAuthenticationResult result)
    {
      var whoami = ExtractFirstLine(result.Message);
      if (result.IsAuthenticated)
      {
        var msg = string.IsNullOrEmpty(whoami)
          ? "Codex is authenticated."
          : whoami;
        await UpdateAuthenticationStateAsync(true, true, msg, false);
      }
      else
      {
        var msg = string.IsNullOrEmpty(whoami)
          ? "Codex login required. Click Login to continue."
          : whoami;
        await UpdateAuthenticationStateAsync(true, false, msg, false);
      }
    }

    private static string ExtractFirstLine(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
        return string.Empty;

      using var reader = new StringReader(value);
      string line;
      while ((line = reader.ReadLine()) != null)
      {
        if (!string.IsNullOrWhiteSpace(line))
          return line.Trim();
      }

      return string.Empty;
    }

    private async void OnLoginClick(object sender, RoutedEventArgs e)
    {
      try
      {
        await EnsureWorkingDirectoryUpToDateAsync("login-click");

        var host = _host;
        if (host == null)
          return;

        await UpdateAuthenticationStateAsync(_authKnown, _isAuthenticated, "Opening Codex login flow...", true);
        var options = _options ?? new CodexOptions();
        var dir = _workingDir ?? string.Empty;

        var ok = await host.LoginAsync(options, dir);
        if (!ok)
        {
          await UpdateAuthenticationStateAsync(true, _isAuthenticated, "codex login failed. Check Diagnostics.", false);
          return;
        }

        await UpdateAuthenticationStateAsync(true, _isAuthenticated, "Restarting Codex CLI...", true);
        var restarted = await RestartCliAsync();
        if (!restarted)
        {
          await UpdateAuthenticationStateAsync(true, false, "CLI restart failed after login. See Diagnostics.", false);
          return;
        }

        await UpdateAuthenticationStateAsync(true, _isAuthenticated, "Confirming Codex login...", true);
        var auth = await _host.CheckAuthenticationAsync(options, dir);
        await HandleAuthenticationResultAsync(auth);
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] OnLoginClick failed: {ex.Message}");
        await UpdateAuthenticationStateAsync(true, _isAuthenticated, "Login failed. Check Diagnostics.", false);
      }
    }

    private async void OnLogoutClick(object sender, RoutedEventArgs e)
    {
      try
      {
        await EnsureWorkingDirectoryUpToDateAsync("logout-click");

        var host = _host;
        if (host == null)
          return;

        await UpdateAuthenticationStateAsync(true, _isAuthenticated, "Signing out of Codex...", true);
        var options = _options ?? new CodexOptions();
        var dir = _workingDir ?? string.Empty;

        var ok = await host.LogoutAsync(options, dir);
        if (!ok)
        {
          await UpdateAuthenticationStateAsync(true, _isAuthenticated, "codex logout failed. Check Diagnostics.", false);
          return;
        }

        await UpdateAuthenticationStateAsync(true, false, "Restarting Codex CLI...", true);
        var restarted = await RestartCliAsync();
        if (!restarted)
        {
          await UpdateAuthenticationStateAsync(true, false, "CLI restart failed after logout. See Diagnostics.", false);
          return;
        }

        await UpdateAuthenticationStateAsync(true, false, "Confirming Codex logout...", true);
        var auth = await _host.CheckAuthenticationAsync(options, dir);
        await HandleAuthenticationResultAsync(auth);
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] OnLogoutClick failed: {ex.Message}");
        await UpdateAuthenticationStateAsync(true, false, "Logout failed. Check Diagnostics.", false);
      }
    }

    private async void HandleStderr(string line)
    {
      try
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[stderr] {line}");
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] HandleStderr failed: {ex.Message}");
      }
    }

    private int _assistantChunkCounter;
    private readonly TelemetryTracker _telemetry = new();

  }
}
