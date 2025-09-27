using System;
using System.Windows;
using System.Windows.Threading;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private void ApplyWindowPreferences()
    {
      Dispatcher.BeginInvoke(new Action(() =>
      {
        var window = Window.GetWindow(this);
        if (window == null)
          return;

        if (!ReferenceEquals(_hostWindow, window))
        {
          UnhookWindowEvents();
          _hostWindow = window;
        }

        ApplyWindowSettings(window);
        HookWindowEvents(window);
      }), DispatcherPriority.Background);
    }

    private void HookWindowEvents(System.Windows.Window window)
    {
      if (window == null || _windowEventsHooked)
        return;

      window.SizeChanged += OnHostWindowSizeChanged;
      window.LocationChanged += OnHostWindowLocationChanged;
      window.StateChanged += OnHostWindowStateChanged;
      _windowEventsHooked = true;
    }

    private void UnhookWindowEvents()
    {
      if (_hostWindow == null || !_windowEventsHooked)
        return;

      _hostWindow.SizeChanged -= OnHostWindowSizeChanged;
      _hostWindow.LocationChanged -= OnHostWindowLocationChanged;
      _hostWindow.StateChanged -= OnHostWindowStateChanged;
      _windowEventsHooked = false;
      _hostWindow = null;
    }

    private void ApplyWindowSettings(System.Windows.Window window)
    {
      if (window == null || _options == null)
        return;

      if (window.WindowState == WindowState.Normal)
      {
        if (_options.WindowWidth > 0)
          window.Width = _options.WindowWidth;
        if (_options.WindowHeight > 0)
          window.Height = _options.WindowHeight;

        if (!double.IsNaN(_options.WindowLeft))
          window.Left = _options.WindowLeft;
        if (!double.IsNaN(_options.WindowTop))
          window.Top = _options.WindowTop;
      }

      if (!string.IsNullOrWhiteSpace(_options.WindowState) &&
          Enum.TryParse(_options.WindowState, out WindowState state))
      {
        window.WindowState = state;
      }
    }

    private void OnHostWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
      if (_options == null)
        return;

      if (sender is Window window && window.WindowState == WindowState.Normal)
      {
        if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
          _options.WindowWidth = e.NewSize.Width;
          _options.WindowHeight = e.NewSize.Height;
          QueueOptionSave();
        }
      }
    }

    private void OnHostWindowLocationChanged(object sender, EventArgs e)
    {
      if (_options == null)
        return;

      if (sender is Window window && window.WindowState == WindowState.Normal)
      {
        _options.WindowLeft = window.Left;
        _options.WindowTop = window.Top;
        QueueOptionSave();
      }
    }

    private void OnHostWindowStateChanged(object sender, EventArgs e)
    {
      if (_options == null)
        return;

      if (sender is Window window)
      {
        _options.WindowState = window.WindowState.ToString();
        if (window.WindowState == WindowState.Normal)
        {
          _options.WindowWidth = window.Width;
          _options.WindowHeight = window.Height;
          _options.WindowLeft = window.Left;
          _options.WindowTop = window.Top;
        }
        QueueOptionSave();
      }
    }
  }
}
