using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using CodexVS22.Core;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private async void OnCopyAllClick(object sender, RoutedEventArgs e)
    {
      try
      {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var text = CollectTranscriptText();
        if (string.IsNullOrWhiteSpace(text))
          return;
        Clipboard.SetText(text);
        await VS.StatusBar.ShowMessageAsync("Transcript copied to clipboard.");
        if (sender is Button button)
        {
          AnimateButtonFeedback(button);
        }
      }
      catch (Exception ex)
      {
        var pane = await DiagnosticsPane.GetAsync();
        await pane.WriteLineAsync($"[error] Copy all failed: {ex.Message}");
      }
    }

    private string CollectTranscriptText()
    {
      if (this.FindName("Transcript") is not StackPanel transcript)
        return string.Empty;

      var builder = new StringBuilder();
      foreach (var child in transcript.Children)
      {
        if (child is FrameworkElement element)
        {
          var text = ExtractTextFromElement(element);
          if (string.IsNullOrWhiteSpace(text))
            continue;
          if (builder.Length > 0)
            builder.AppendLine().AppendLine();
          builder.Append(text.TrimEnd());
        }
      }

      return builder.ToString();
    }

    private static string ExtractTextFromElement(FrameworkElement element)
    {
      switch (element)
      {
        case null:
          return string.Empty;
        case TextBlock tb:
          return tb.Text ?? string.Empty;
        case Border border:
          return ExtractTextFromElement(border.Child as FrameworkElement);
        case StackPanel panel:
          return ExtractTextFromPanel(panel);
        default:
          return string.Empty;
      }
    }

    private static string ExtractTextFromPanel(StackPanel panel)
    {
      if (panel == null)
        return string.Empty;

      var builder = new StringBuilder();
      foreach (var child in panel.Children)
      {
        if (child is FrameworkElement element)
        {
          var text = ExtractTextFromElement(element);
          if (string.IsNullOrWhiteSpace(text))
            continue;
          if (builder.Length > 0)
            builder.AppendLine();
          builder.Append(text.TrimEnd());
        }
      }

      return builder.ToString();
    }

    private static void AnimateButtonFeedback(Button button)
    {
      var animation = new DoubleAnimation(1.0, 0.6, TimeSpan.FromMilliseconds(140))
      {
        AutoReverse = true,
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
      };
      button.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    private static void AnimateBubbleFeedback(TextBlock bubble)
    {
      var baseBrush = bubble.Background as SolidColorBrush;
      if (baseBrush == null || baseBrush.IsFrozen)
      {
        baseBrush = new SolidColorBrush(Colors.Transparent);
        bubble.Background = baseBrush;
      }

      var animation = new ColorAnimation
      {
        From = Colors.Transparent,
        To = Color.FromArgb(80, 0, 120, 215),
        Duration = TimeSpan.FromMilliseconds(120),
        AutoReverse = true,
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
      };

      baseBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
    }
  }
}
