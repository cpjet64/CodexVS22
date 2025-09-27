using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CodexVS22
{
  public partial class MyToolWindowControl
  {
    private static string CreateHeartbeatSubmission(JObject opTemplate)
    {
      if (opTemplate == null)
        return string.Empty;

      var op = opTemplate.DeepClone() as JObject;
      if (op == null)
        return string.Empty;

      var type = TryGetString(op, "type");
      if (string.IsNullOrWhiteSpace(type))
        return string.Empty;

      var submission = new JObject
      {
        ["id"] = Guid.NewGuid().ToString(),
        ["op"] = op
      };

      return submission.ToString(Formatting.None);
    }

    private static HeartbeatState ExtractHeartbeatState(JObject raw)
    {
      if (raw == null)
        return null;

      var heartbeatToken = FindHeartbeatToken(raw);
      var intervalMs = ExtractHeartbeatIntervalMs(raw, heartbeatToken);
      if (intervalMs <= 0)
        return null;

      var opTemplate = BuildHeartbeatOpTemplate(raw, heartbeatToken);
      if (opTemplate == null)
        return null;

      var opType = TryGetString(opTemplate, "type");
      if (string.IsNullOrWhiteSpace(opType))
        return null;

      var interval = TimeSpan.FromMilliseconds(Math.Max(intervalMs, 1000));
      return new HeartbeatState(interval, opTemplate, opType);
    }

    private static JToken FindHeartbeatToken(JObject root)
    {
      if (root == null)
        return null;

      foreach (var path in new[]
      {
        "heartbeat",
        "session.heartbeat",
        "session.capabilities.heartbeat",
        "session.protocol_features.heartbeat",
        "capabilities.heartbeat",
        "protocol_features.heartbeat",
        "features.heartbeat",
        "settings.heartbeat"
      })
      {
        var token = SafeSelectToken(root, path);
        if (token != null)
          return token;
      }

      return null;
    }

    private static int ExtractHeartbeatIntervalMs(JObject root, JToken heartbeatToken)
    {
      if (heartbeatToken != null)
      {
        if (heartbeatToken.Type == JTokenType.Integer || heartbeatToken.Type == JTokenType.Float)
        {
          var direct = ValueAsInt(heartbeatToken);
          if (direct > 0)
            return direct;
        }

        if (heartbeatToken is JObject heartbeatObj)
        {
          foreach (var name in new[] { "interval_ms", "intervalMs", "interval" })
          {
            var value = ValueAsInt(heartbeatObj[name]);
            if (value > 0)
              return value;
          }

          if (heartbeatObj["config"] is JObject configObj)
          {
            foreach (var name in new[] { "interval_ms", "intervalMs" })
            {
              var value = ValueAsInt(configObj[name]);
              if (value > 0)
                return value;
            }
          }
        }
      }

      foreach (var token in new[]
      {
        root,
        root?["session"] as JObject
      })
      {
        if (token == null)
          continue;

        foreach (var name in new[]
        {
          "heartbeat_interval_ms",
          "heartbeatIntervalMs",
          "keep_alive_ms",
          "keepAliveMs",
          "keepalive_ms"
        })
        {
          var value = ValueAsInt(token[name]);
          if (value > 0)
            return value;
        }
      }

      return 0;
    }

    private static JObject BuildHeartbeatOpTemplate(JObject root, JToken heartbeatToken)
    {
      JObject opTemplate = null;
      if (heartbeatToken is JObject heartbeatObj)
      {
        if (heartbeatObj["op"] is JObject opObj)
          opTemplate = opObj.DeepClone() as JObject;
        else if (heartbeatObj["op_template"] is JObject opTemplateObj)
          opTemplate = opTemplateObj.DeepClone() as JObject;
        else if (heartbeatObj["opTemplate"] is JObject opTemplateCamel)
          opTemplate = opTemplateCamel.DeepClone() as JObject;
      }

      if (opTemplate != null)
      {
        var type = TryGetString(opTemplate, "type");
        if (string.IsNullOrWhiteSpace(type))
        {
          var fallback = ExtractOpTypeFromToken(heartbeatToken as JObject) ?? DetermineFallbackOpType(root);
          if (string.IsNullOrWhiteSpace(fallback))
            return null;
          opTemplate["type"] = fallback;
        }

        return opTemplate;
      }

      var opType = ExtractOpTypeFromToken(heartbeatToken as JObject) ?? DetermineFallbackOpType(root);
      if (string.IsNullOrWhiteSpace(opType))
        return null;

      return new JObject { ["type"] = opType };
    }

    private static string ExtractOpTypeFromToken(JObject token)
    {
      if (token == null)
        return null;

      var opType = TryGetString(token, "op_type") ?? TryGetString(token, "opType");
      if (!string.IsNullOrWhiteSpace(opType))
        return opType;

      if (token["op"] is JObject op)
      {
        opType = TryGetString(op, "type");
        if (!string.IsNullOrWhiteSpace(opType))
          return opType;
      }

      return null;
    }

    private static string DetermineFallbackOpType(JObject root)
    {
      var supported = ExtractSupportedOps(root);
      if (supported.Contains("heartbeat"))
        return "heartbeat";
      if (supported.Contains("noop"))
        return "noop";
      return null;
    }

    private static IReadOnlyCollection<string> ExtractSupportedOps(JObject root)
    {
      var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      if (root == null)
        return result;

      foreach (var name in new[]
      {
        "supported_ops",
        "supportedOps",
        "supported_operations",
        "supportedOperations",
        "allowed_ops",
        "allowedOps"
      })
      {
        foreach (var token in FindTokensByName(root, name))
        {
          if (token == null)
            continue;

          if (token.Type == JTokenType.Array)
          {
            foreach (var item in token)
            {
              var value = item?.ToString();
              if (!string.IsNullOrWhiteSpace(value))
                result.Add(value.Trim());
            }
          }
          else
          {
            var value = token.ToString();
            if (!string.IsNullOrWhiteSpace(value))
              result.Add(value.Trim());
          }
        }
      }

      return result;
    }

    private static IEnumerable<JToken> FindTokensByName(JToken token, string name)
    {
      if (token == null)
        yield break;

      if (token.Type == JTokenType.Object)
      {
        var obj = (JObject)token;
        foreach (var property in obj.Properties())
        {
          if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            yield return property.Value;

          foreach (var child in FindTokensByName(property.Value, name))
            yield return child;
        }
      }
      else if (token.Type == JTokenType.Array)
      {
        foreach (var item in (JArray)token)
        {
          foreach (var child in FindTokensByName(item, name))
            yield return child;
        }
      }
    }

    private static int ValueAsInt(JToken token)
    {
      if (token == null || token.Type == JTokenType.Null)
        return 0;

      if (token.Type == JTokenType.Integer)
        return token.Value<int>();

      if (token.Type == JTokenType.Float)
        return (int)Math.Round(token.Value<double>());

      var text = token.ToString();
      if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        return parsed;

      return 0;
    }
  }
}