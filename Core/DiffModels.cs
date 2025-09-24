using System;

namespace CodexVS22.Core
{
  // Public top-level types so XAML code-behind can reference them during tmp builds.
  public sealed class DiffDocument
  {
    public DiffDocument(string path, string original, string modified)
    {
      Path = string.IsNullOrWhiteSpace(path) ? "(untitled)" : path;
      Original = original ?? string.Empty;
      Modified = modified ?? string.Empty;
      IsEmpty = string.IsNullOrWhiteSpace(Original) && string.IsNullOrWhiteSpace(Modified);
      IsBinary = LooksBinary(Original) || LooksBinary(Modified);
    }

    public string Path { get; }
    public string Original { get; }
    public string Modified { get; }
    public bool IsEmpty { get; }
    public bool IsBinary { get; }

    private static bool LooksBinary(string content)
    {
      if (string.IsNullOrEmpty(content))
        return false;

      var sampleLength = Math.Min(content.Length, 512);
      var controlCount = 0;

      for (var i = 0; i < sampleLength; i++)
      {
        var ch = content[i];
        if (ch == '\0')
          return true;

        if (char.IsControl(ch) && ch != '\r' && ch != '\n' && ch != '\t')
          controlCount++;
      }

      return controlCount > sampleLength * 0.1;
    }
  }

  public enum PatchApplyResult
  {
    Applied,
    Conflict,
    Failed
  }
}

