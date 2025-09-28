using System.Collections.ObjectModel;

namespace CodexVS22.ToolWindows.CodexToolWindow.ViewModels
{
    /// <summary>
    /// Placeholder chat transcript view-model exposing minimal observable collection.
    /// </summary>
    public class ChatTranscriptViewModel
    {
        public ObservableCollection<string> TranscriptItems { get; } = new();
    }
}
