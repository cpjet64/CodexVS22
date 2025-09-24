using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace CodexVS22.Core
{
  internal static class DiffUtilities
  {

    internal static List<DiffDocument> ExtractDocuments(JObject obj)
    {
      var results = new List<DiffDocument>();
      if (obj == null)
        return results;

      if (obj["files"] is JArray filesArray)
      {
        foreach (var token in filesArray.OfType<JObject>())
        {
          var path = TryGetString(token, "path") ??
                     TryGetString(token, "file") ??
                     TryGetString(token, "relative_path") ??
                     TryGetString(token, "filename") ??
                     TryGetString(token, "target") ??
                     string.Empty;

          var original = ExtractDocumentText(token, new[] { "original", "previous", "before", "old", "base", "left" });
          var modified = ExtractDocumentText(token, new[] { "text", "content", "after", "new", "current", "right" });

          if (string.IsNullOrEmpty(modified))
            modified = ExtractNestedText(token, "data", "text");

          if (string.IsNullOrEmpty(original))
            original = string.Empty;

          results.Add(new DiffDocument(path, original, modified ?? string.Empty));
        }
        return results;
      }

      var diffText = TryGetString(obj, "text") ?? TryGetString(obj, "diff") ?? TryGetString(obj, "patch");
      if (!string.IsNullOrEmpty(diffText))
        results.Add(new DiffDocument("codex.diff", string.Empty, diffText));

      return results;
    }

    internal static string NormalizeFileContent(string content)
    {
      if (content == null)
        return string.Empty;

      var normalized = content.Replace("\r\n", "\n");
      normalized = normalized.Replace("\r", "\n");
      return normalized.Replace("\n", Environment.NewLine);
    }

    internal static string NormalizeForComparison(string content)
    {
      if (content == null)
        return string.Empty;

      return content
        .Replace("\r\n", "\n")
        .Replace("\r", "\n")
        .TrimEnd();
    }

    internal static PatchApplyResult ApplyPatchToFileForTests(string path, string original, string modified)
    {
      if (string.IsNullOrWhiteSpace(path))
        throw new ArgumentException("Path must be provided", nameof(path));

      var current = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
      if (File.Exists(path))
      {
        try
        {
          var info = new FileInfo(path);
          if (info.IsReadOnly)
            return PatchApplyResult.Failed;
        }
        catch
        {
          // treat errors as failure to avoid modification
          return PatchApplyResult.Failed;
        }
      }
      if (!string.IsNullOrEmpty(original))
      {
        var normalizedCurrent = NormalizeForComparison(current);
        var normalizedOriginal = NormalizeForComparison(original);
        if (!string.Equals(normalizedCurrent, normalizedOriginal, StringComparison.Ordinal))
          return PatchApplyResult.Conflict;
      }

      var normalized = NormalizeFileContent(modified ?? string.Empty);
      File.WriteAllText(path, normalized, Encoding.UTF8);
      return PatchApplyResult.Applied;
    }

    private static string TryGetString(JObject obj, string name)
    {
      try { return obj?[name]?.ToString(); } catch { return null; }
    }

    private static string ExtractDocumentText(JObject token, IEnumerable<string> keys)
    {
      foreach (var key in keys)
      {
        var value = TryGetString(token, key);
        if (!string.IsNullOrEmpty(value))
          return value;
      }

      return string.Empty;
    }

    private static string ExtractNestedText(JObject token, params string[] path)
    {
      JToken current = token;
      foreach (var segment in path)
      {
        if (current is JObject obj)
          current = obj[segment];
        else
          return string.Empty;
      }

      return current?.ToString() ?? string.Empty;
    }

    // Binary detection moved to DiffDocument; keep helper here for any future needs.
  }
}
