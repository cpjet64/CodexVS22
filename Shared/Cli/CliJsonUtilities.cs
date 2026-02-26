using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CodexVS22.Shared.Cli
{
    public static class CliJsonUtilities
    {
        public static string TryGetString(JObject obj, params string[] names)
        {
            if (obj == null || names == null || names.Length == 0)
                return null;

            foreach (var name in names)
            {
                if (string.IsNullOrEmpty(name))
                    continue;

                try
                {
                    if (!obj.TryGetValue(name, out var token) || token == null || token.Type == JTokenType.Null)
                        continue;

                    var value = token.Type == JTokenType.String
                        ? token.Value<string>()
                        : token.ToString(Formatting.None);

                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
                catch
                {
                    // ignore malformed tokens
                }
            }

            return null;
        }

        public static int? TryGetInt(JObject obj, params string[] names)
        {
            if (obj == null || names == null)
                return null;

            foreach (var name in names)
            {
                if (string.IsNullOrEmpty(name))
                    continue;

                var token = obj[name];
                if (TryReadInt(token, out var value))
                    return value;

                var text = token?.ToString();
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                    return value;
            }

            return null;
        }

        public static bool? TryGetBoolean(JObject obj, params string[] names)
        {
            if (obj == null || names == null)
                return null;

            foreach (var name in names)
            {
                if (string.IsNullOrEmpty(name))
                    continue;

                var token = obj[name];
                if (token == null)
                    continue;

                if (token.Type == JTokenType.Boolean)
                    return token.Value<bool>();

                if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
                    return Math.Abs(token.Value<double>()) > double.Epsilon;

                var text = token.ToString().Trim();
                if (bool.TryParse(text, out var boolean))
                    return boolean;

                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
                    return numeric != 0;

                if (string.Equals(text, "success", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, "completed", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (string.Equals(text, "failed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, "error", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return null;
        }

        public static int? TryGetInt(JToken token)
        {
            if (TryReadInt(token, out var value))
                return value;
            return null;
        }

        private static bool TryReadInt(JToken token, out int value)
        {
            value = 0;
            if (token == null)
                return false;

            if (token.Type == JTokenType.Integer)
            {
                value = token.Value<int>();
                return true;
            }

            if (token.Type == JTokenType.Float)
            {
                value = (int)Math.Round(token.Value<double>());
                return true;
            }

            var text = token.ToString();
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return true;

            return false;
        }

        public static JToken SafeSelectToken(JObject obj, string path)
        {
            try { return obj?.SelectToken(path, false); }
            catch { return null; }
        }
    }
}
