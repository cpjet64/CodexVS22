using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CodexVS22.Core
{
  internal static class ChatTextUtilities
  {
    private static readonly Regex AnsiRegex = new(@"\x1B\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);

    public static string CreateUserInputSubmission(string text)
    {
      var submission = new JObject
      {
        ["id"] = Guid.NewGuid().ToString(),
        ["op"] = new JObject
        {
          ["type"] = "user_input",
          ["items"] = new JArray
          {
            new JObject
            {
              ["type"] = "text",
              ["text"] = text ?? string.Empty
            }
          }
        }
      };

      return submission.ToString(Formatting.None);
    }

    public static string NormalizeAssistantText(string value)
    {
      if (string.IsNullOrEmpty(value))
        return string.Empty;

      var stripped = StripAnsi(value);
      var normalized = stripped.Normalize(NormalizationForm.FormKC);
      normalized = FixMojibake(normalized);

      var builder = new StringBuilder(normalized.Length);
      var enumerator = StringInfo.GetTextElementEnumerator(normalized);
      while (enumerator.MoveNext())
      {
        var element = enumerator.GetTextElement();
        if (element.Length == 1)
        {
          var ch = element[0];
          builder.Append(ch switch
          {
            '\u2018' or '\u2019' or '\u2032' => '\'',
            '\u201C' or '\u201D' or '\u2033' => '"',
            '\u2013' or '\u2014' => '-',
            '\u2026' => "...",
            '\u00A0' or '\u2007' or '\u205F' => ' ',
            '\uFEFF' => ' ',
            _ => ch
          });
        }
        else
        {
          builder.Append(element switch
          {
            "\u200D" => string.Empty,
            _ => element
          });
        }
      }

      return builder.ToString();
    }

    public static string StripAnsi(string value)
    {
      if (string.IsNullOrEmpty(value))
        return string.Empty;

      return AnsiRegex.Replace(value, string.Empty);
    }

    private static string FixMojibake(string value)
    {
      if (string.IsNullOrEmpty(value))
        return string.Empty;

      value = value.Replace("\uFFFD", "");
      value = value.Replace("â€™", "'")
                   .Replace("â€œ", "\"")
                   .Replace("â€", "\"")
                   .Replace("â€“", "-")
                   .Replace("â€”", "-")
                   .Replace("â€¦", "...");

      if (value.IndexOf("Ã", StringComparison.Ordinal) >= 0)
      {
        try
        {
          var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(value);
          var recoded = Encoding.UTF8.GetString(bytes);
          if (!string.IsNullOrEmpty(recoded))
            value = recoded;
        }
        catch
        {
          // best effort fallback
        }
      }

      return value;
    }
  }
}
