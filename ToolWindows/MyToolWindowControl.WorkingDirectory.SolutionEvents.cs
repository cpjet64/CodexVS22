using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private async Task CleanupSolutionSubscriptionsAsync()
    {
      await UnadviseSolutionEventsAsync();
      await UnsubscribeUiContextsAsync();
    }

    private sealed class SolutionEventsSink : IVsSolutionEvents
    {
      private readonly WeakReference<MyToolWindowControl> _owner;

      public SolutionEventsSink(MyToolWindowControl owner)
      {
        _owner = new WeakReference<MyToolWindowControl>(owner);
      }

      private void Notify(string reason, Action<MyToolWindowControl> callback = null)
      {
        if (_owner.TryGetTarget(out var control))
        {
          control.OnSolutionContextChanged(reason);
          callback?.Invoke(control);
        }
      }

      public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
      {
        if (fAdded != 0)
          Notify("project-opened");
        return VSConstants.S_OK;
      }

      public int OnAfterCloseProject(IVsHierarchy pHierarchy, int fRemoved)
      {
        if (fRemoved != 0)
          Notify("project-closed");
        return VSConstants.S_OK;
      }

      public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) => VSConstants.S_OK;

      public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved) => VSConstants.S_OK;

      public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
      {
        Notify("project-loaded");
        return VSConstants.S_OK;
      }

      public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) => VSConstants.S_OK;

      public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
      {
        Notify("project-unload");
        return VSConstants.S_OK;
      }

      public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
      {
        Notify("solution-opened", control => control.OnSolutionEventsSolutionOpened());
        return VSConstants.S_OK;
      }

      public int OnBeforeCloseSolution(object pUnkReserved) => VSConstants.S_OK;

      public int OnAfterCloseSolution(object pUnkReserved)
      {
        Notify("solution-closed", control => control.OnSolutionClosed());
        return VSConstants.S_OK;
      }

      public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) => VSConstants.S_OK;
    }


  }
}
