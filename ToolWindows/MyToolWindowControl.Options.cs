using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private async Task InitializeSelectorsAsync()
    {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

      _initializingSelectors = true;
      try
      {
        var approvalBox = this.FindName("ApprovalCombo") as ComboBox;
        if (approvalBox != null)
        {
          approvalBox.SelectionChanged -= OnApprovalModeChanged;
          var index = Array.IndexOf(ApprovalModeOptions, _selectedApprovalMode);
          approvalBox.SelectedIndex = index >= 0 ? index : 0;
          approvalBox.SelectionChanged += OnApprovalModeChanged;
        }

        var modelBox = this.FindName("ModelCombo") as ComboBox;
        if (modelBox != null)
        {
          modelBox.SelectionChanged -= OnModelSelectionChanged;
          modelBox.ItemsSource = ModelOptions;
          _selectedModel = NormalizeModel(_options?.DefaultModel);
          modelBox.SelectedItem = _selectedModel;
          modelBox.SelectionChanged += OnModelSelectionChanged;
        }

        var reasoningBox = this.FindName("ReasoningCombo") as ComboBox;
        if (reasoningBox != null)
        {
          reasoningBox.SelectionChanged -= OnReasoningSelectionChanged;
          reasoningBox.ItemsSource = ReasoningOptions;
          _selectedReasoning = NormalizeReasoning(_options?.DefaultReasoning);
          reasoningBox.SelectedItem = _selectedReasoning;
          reasoningBox.SelectionChanged += OnReasoningSelectionChanged;
        }
      }
      finally
      {
        _initializingSelectors = false;
      }
    }

    private async Task RestoreLastUsedItemsAsync()
    {
      try
      {
        // Restore last used prompt if available
        if (!string.IsNullOrEmpty(_options?.LastUsedPrompt))
        {
          var lastPrompt = _customPrompts.FirstOrDefault(p => p.Id == _options.LastUsedPrompt);
          if (lastPrompt != null)
          {
            // Highlight the last used prompt in the UI
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            // Could add visual indication here if needed
          }
        }

        // Restore last used tool if available
        if (!string.IsNullOrEmpty(_options?.LastUsedTool))
        {
          var lastTool = _mcpTools.FirstOrDefault(t => t.Name == _options.LastUsedTool);
          if (lastTool != null)
          {
            // Highlight the last used tool in the UI
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            // Could add visual indication here if needed
          }
        }
      }
      catch (Exception ex)
      {
        // Log error but don't fail initialization
        _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => 
        {
          await LogTelemetryAsync("restore_last_used_failed", new Dictionary<string, object>
          {
            ["error"] = ex.Message
          });
        });
      }
    }

    private static string NormalizeModel(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
        return DefaultModelName;

      foreach (var option in ModelOptions)
      {
        if (string.Equals(option, value, StringComparison.OrdinalIgnoreCase))
          return option;
      }

      return DefaultModelName;
    }

    private static string NormalizeReasoning(string value)
    {
      if (string.IsNullOrWhiteSpace(value))
        return DefaultReasoningValue;

      foreach (var option in ReasoningOptions)
      {
        if (string.Equals(option, value, StringComparison.OrdinalIgnoreCase))
          return option;
      }

      return DefaultReasoningValue;
    }

    private void OnModelSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
      if (_initializingSelectors)
        return;

      var selected = (sender as ComboBox)?.SelectedItem as string;
      var normalized = NormalizeModel(selected ?? string.Empty);
      if (string.Equals(normalized, _selectedModel, StringComparison.Ordinal))
        return;

      _selectedModel = normalized;
      if (_options != null)
        _options.DefaultModel = normalized;
      QueueOptionSave();
    }

    private void OnReasoningSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
      if (_initializingSelectors)
        return;

      var selected = (sender as ComboBox)?.SelectedItem as string;
      var normalized = NormalizeReasoning(selected ?? string.Empty);
      if (string.Equals(normalized, _selectedReasoning, StringComparison.Ordinal))
        return;

      _selectedReasoning = normalized;
      if (_options != null)
        _options.DefaultReasoning = normalized;
      QueueOptionSave();
    }

    private void OnApprovalModeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
      if (_initializingSelectors)
        return;

      if (sender is not ComboBox combo || combo.SelectedIndex < 0 || combo.SelectedIndex >= ApprovalModeOptions.Length)
        return;

      var mode = ApprovalModeOptions[combo.SelectedIndex];
      if (mode == _selectedApprovalMode)
        return;

      _selectedApprovalMode = mode;
      if (_options != null)
        _options.Mode = mode;
      QueueOptionSave();
      EnqueueFullAccessBannerRefresh();
    }

    private static void QueueOptionSave()
    {
      var options = CodexVS22Package.OptionsInstance;
      if (options == null)
        return;

      ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
      {
        try
        {
          await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
          options.SaveSettingsToStorage();
        }
        catch
        {
          // ignore persistence failures; options page will handle fallback
        }
      });
    }
  }
}
