#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace UnityMCP.Shared
{
    public enum JsonValueKind
    {
        Undefined,
        Object,
        Array,
        String,
        Number,
        True,
        False,
        Null
    }

    public struct JsonElement
    {
        readonly object _value;

        public JsonElement(object value)
        {
            _value = value;
        }

        public JsonValueKind ValueKind
        {
            get
            {
                if (_value == null) return JsonValueKind.Null;
                if (_value is Dictionary<string, JsonElement>) return JsonValueKind.Object;
                if (_value is List<JsonElement>) return JsonValueKind.Array;
                if (_value is string) return JsonValueKind.String;
                if (_value is bool b) return b ? JsonValueKind.True : JsonValueKind.False;
                if (_value is int || _value is long || _value is float || _value is double || _value is decimal) return JsonValueKind.Number;
                return JsonValueKind.Undefined;
            }
        }

        public bool TryGetProperty(string name, out JsonElement value)
        {
            if (_value is Dictionary<string, JsonElement> dict && dict.TryGetValue(name, out value)) return true;
            value = default;
            return false;
        }

        public IEnumerable<JsonElement> EnumerateArray()
            => _value is List<JsonElement> list ? list : Enumerable.Empty<JsonElement>();

        public string GetString() => _value as string ?? _value?.ToString();
        public bool GetBoolean() => _value is bool b && b;

        public bool TryGetInt32(out int result)
        {
            if (_value is int i) { result = i; return true; }
            if (_value is long l && l >= int.MinValue && l <= int.MaxValue) { result = (int)l; return true; }
            if (_value is double d && d >= int.MinValue && d <= int.MaxValue) { result = (int)d; return true; }
            return int.TryParse(GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        public bool TryGetInt64(out long result)
        {
            if (_value is long l) { result = l; return true; }
            if (_value is int i) { result = i; return true; }
            if (_value is double d && d >= long.MinValue && d <= long.MaxValue) { result = (long)d; return true; }
            return long.TryParse(GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        public bool TryGetSingle(out float result)
        {
            if (_value is float f) { result = f; return true; }
            if (_value is double d) { result = (float)d; return true; }
            if (_value is int i) { result = i; return true; }
            if (_value is long l) { result = l; return true; }
            return float.TryParse(GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        public bool TryGetDouble(out double result)
        {
            if (_value is double d) { result = d; return true; }
            if (_value is float f) { result = f; return true; }
            if (_value is int i) { result = i; return true; }
            if (_value is long l) { result = l; return true; }
            return double.TryParse(GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        public float GetSingle()
        {
            TryGetSingle(out var result);
            return result;
        }

        public string GetRawText() => SimpleJson.SerializeObject(ToObject());

        internal object ToObject()
        {
            if (_value is Dictionary<string, JsonElement> dict) return dict.ToDictionary(kv => kv.Key, kv => kv.Value.ToObject());
            if (_value is List<JsonElement> list) return list.Select(item => item.ToObject()).ToArray();
            return _value;
        }
    }

    public sealed class JsonDocument : IDisposable
    {
        public JsonElement RootElement { get; private set; }

        JsonDocument(JsonElement rootElement)
        {
            RootElement = rootElement;
        }

        public static JsonDocument Parse(string json) => new JsonDocument(SimpleJson.ParseElement(json));

        public void Dispose()
        {
        }
    }

    public sealed class JsonSerializerOptions
    {
        public bool WriteIndented { get; set; }
    }

    public static class JsonSerializer
    {
        public static T Deserialize<T>(string json)
        {
            var value = SimpleJson.ParseElement(json).ToObject();
            if (value == null) return default;
            if (value is T typed) return typed;

            try
            {
                return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
            }
            catch
            {
                return default;
            }
        }

        public static string Serialize(object obj, JsonSerializerOptions options = null) => SimpleJson.SerializeObject(obj);
    }

    /// <summary>
    /// Context passed to tool handlers with request info and cancellation.
    /// </summary>
    public sealed class ToolContext
    {
        public string RequestId { get; }
        public string ArgumentsJson { get; }
        public JsonElement Arguments { get; }
        public CancellationToken CancellationToken { get; }

        public ToolContext(string requestId, string argumentsJson, CancellationToken cancellationToken)
        {
            RequestId = requestId;
            ArgumentsJson = argumentsJson ?? "{}";
            Arguments = SimpleJson.ParseElement(ArgumentsJson);
            CancellationToken = cancellationToken;
        }

        public string GetString(string name, string defaultValue = "")
            => SimpleJson.GetString(ArgumentsJson, name, defaultValue);

        public int GetInt(string name, int defaultValue = 0)
            => SimpleJson.GetInt(ArgumentsJson, name, defaultValue);

        public float GetFloat(string name, float defaultValue = 0f)
            => SimpleJson.GetFloat(ArgumentsJson, name, defaultValue);

        public bool GetBool(string name, bool defaultValue = false)
            => SimpleJson.GetBool(ArgumentsJson, name, defaultValue);
    }

    /// <summary>
    /// Simple JSON parser - no external dependencies.
    /// </summary>
    public static class SimpleJson
    {
        public static JsonElement ParseElement(string json)
        {
            var parser = new Parser(json ?? "null");
            return new JsonElement(parser.ParseValue());
        }

        public static string GetString(string json, string key, string defaultValue = "")
        {
            var value = GetRawValue(json, key);
            if (value == null) return defaultValue;
            if (value.StartsWith("\"") && value.EndsWith("\""))
                value = value.Substring(1, value.Length - 2);
            return UnescapeJson(value);
        }

        public static int GetInt(string json, string key, int defaultValue = 0)
        {
            var value = GetRawValue(json, key);
            if (value != null && int.TryParse(value, out var result)) return result;
            return defaultValue;
        }

        public static float GetFloat(string json, string key, float defaultValue = 0f)
        {
            var value = GetRawValue(json, key);
            if (value != null && float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var result)) return result;
            return defaultValue;
        }

        public static bool GetBool(string json, string key, bool defaultValue = false)
        {
            var value = GetRawValue(json, key);
            if (value == "true") return true;
            if (value == "false") return false;
            return defaultValue;
        }

        public static string GetRawObject(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return null;

            var searchKey = $"\"{key}\"";
            var keyIndex = json.IndexOf(searchKey, StringComparison.Ordinal);
            if (keyIndex < 0) return null;

            var colonIndex = json.IndexOf(':', keyIndex + searchKey.Length);
            if (colonIndex < 0) return null;

            var valueStart = colonIndex + 1;
            while (valueStart < json.Length && json[valueStart] == ' ') valueStart++;

            if (valueStart >= json.Length) return null;

            if (json[valueStart] == '{' || json[valueStart] == '[')
            {
                var end = FindMatchingBrace(json, valueStart);
                if (end < 0) return null;
                return json.Substring(valueStart, end - valueStart + 1);
            }

            return GetRawValue(json, key);
        }

        public static string SerializeObject(object obj)
        {
            return SerializeObject(obj, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
        }

        static string SerializeObject(object obj, int depth, HashSet<object> visited)
        {
            if (obj == null) return "null";
            if (obj is string s) return "\"" + EscapeJson(s) + "\"";
            if (obj is bool b) return b ? "true" : "false";
            if (obj is int || obj is long || obj is float || obj is double || obj is decimal || obj is byte || obj is short || obj is ushort || obj is uint || obj is ulong)
                return Convert.ToString(obj, CultureInfo.InvariantCulture);
            if (obj is DateTime dateTime)
                return "\"" + EscapeJson(dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)) + "\"";
            if (obj is DateTimeOffset dateTimeOffset)
                return "\"" + EscapeJson(dateTimeOffset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)) + "\"";
            if (obj is Enum enumValue)
                return "\"" + EscapeJson(enumValue.ToString()) + "\"";

            // Unity struct direct serialization — prevents infinite recursion via property cycles
            // (e.g. Vector3.normalized → Vector3 → normalized → …)
            if (obj is UnityEngine.Vector2 v2)
                return $"{{\"x\":{v2.x.ToString(CultureInfo.InvariantCulture)},\"y\":{v2.y.ToString(CultureInfo.InvariantCulture)}}}";
            if (obj is UnityEngine.Vector3 v3)
                return $"{{\"x\":{v3.x.ToString(CultureInfo.InvariantCulture)},\"y\":{v3.y.ToString(CultureInfo.InvariantCulture)},\"z\":{v3.z.ToString(CultureInfo.InvariantCulture)}}}";
            if (obj is UnityEngine.Vector4 v4)
                return $"{{\"x\":{v4.x.ToString(CultureInfo.InvariantCulture)},\"y\":{v4.y.ToString(CultureInfo.InvariantCulture)},\"z\":{v4.z.ToString(CultureInfo.InvariantCulture)},\"w\":{v4.w.ToString(CultureInfo.InvariantCulture)}}}";
            if (obj is UnityEngine.Quaternion q)
                return $"{{\"x\":{q.x.ToString(CultureInfo.InvariantCulture)},\"y\":{q.y.ToString(CultureInfo.InvariantCulture)},\"z\":{q.z.ToString(CultureInfo.InvariantCulture)},\"w\":{q.w.ToString(CultureInfo.InvariantCulture)}}}";
            if (obj is UnityEngine.Color c)
                return $"{{\"r\":{c.r.ToString(CultureInfo.InvariantCulture)},\"g\":{c.g.ToString(CultureInfo.InvariantCulture)},\"b\":{c.b.ToString(CultureInfo.InvariantCulture)},\"a\":{c.a.ToString(CultureInfo.InvariantCulture)}}}";
            if (obj is UnityEngine.Color32 c32)
                return $"{{\"r\":{c32.r},\"g\":{c32.g},\"b\":{c32.b},\"a\":{c32.a}}}";
            if (obj is UnityEngine.Matrix4x4 m)
                return $"{{\"m00\":{m.m00.ToString(CultureInfo.InvariantCulture)},\"m01\":{m.m01.ToString(CultureInfo.InvariantCulture)},\"m02\":{m.m02.ToString(CultureInfo.InvariantCulture)},\"m03\":{m.m03.ToString(CultureInfo.InvariantCulture)},\"m10\":{m.m10.ToString(CultureInfo.InvariantCulture)},\"m11\":{m.m11.ToString(CultureInfo.InvariantCulture)},\"m12\":{m.m12.ToString(CultureInfo.InvariantCulture)},\"m13\":{m.m13.ToString(CultureInfo.InvariantCulture)},\"m20\":{m.m20.ToString(CultureInfo.InvariantCulture)},\"m21\":{m.m21.ToString(CultureInfo.InvariantCulture)},\"m22\":{m.m22.ToString(CultureInfo.InvariantCulture)},\"m23\":{m.m23.ToString(CultureInfo.InvariantCulture)},\"m30\":{m.m30.ToString(CultureInfo.InvariantCulture)},\"m31\":{m.m31.ToString(CultureInfo.InvariantCulture)},\"m32\":{m.m32.ToString(CultureInfo.InvariantCulture)},\"m33\":{m.m33.ToString(CultureInfo.InvariantCulture)}}}";
            if (obj is UnityEngine.Bounds bounds)
                return $"{{\"center\":{SerializeObject(bounds.center, depth + 1, visited)},\"size\":{SerializeObject(bounds.size, depth + 1, visited)}}}";
            if (obj is UnityEngine.Rect rect)
                return $"{{\"x\":{rect.x.ToString(CultureInfo.InvariantCulture)},\"y\":{rect.y.ToString(CultureInfo.InvariantCulture)},\"width\":{rect.width.ToString(CultureInfo.InvariantCulture)},\"height\":{rect.height.ToString(CultureInfo.InvariantCulture)}}}";

            if (depth > 6) return "null";

            var type = obj.GetType();
            if (!type.IsValueType && !visited.Add(obj))
                return "null";

            if (obj is System.Collections.IDictionary dict)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("{");
                bool first = true;
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append("\"").Append(EscapeJson(entry.Key.ToString())).Append("\":");
                    sb.Append(SerializeObject(entry.Value, depth + 1, visited));
                }
                sb.Append("}");
                return sb.ToString();
            }

            if (obj is System.Collections.IEnumerable enumerable && !(obj is string))
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("[");
                bool first = true;
                foreach (var item in enumerable)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append(SerializeObject(item, depth + 1, visited));
                }
                sb.Append("]");
                return sb.ToString();
            }

            // Reflection fallback for simple objects
            var sb2 = new System.Text.StringBuilder();
            sb2.Append("{");
            bool first2 = true;
            foreach (var prop in type.GetProperties())
            {
                if (!prop.CanRead) continue;
                if (prop.GetIndexParameters().Length > 0) continue;
                // Skip properties that return the same value type to break struct cycles
                // (e.g. Vector3.normalized → Vector3 → normalized → …)
                var propType = prop.PropertyType;
                if (type.IsValueType && propType == type) continue;
                try
                {
                    var value = prop.GetValue(obj);
                    if (!first2) sb2.Append(",");
                    first2 = false;
                    sb2.Append("\"").Append(prop.Name).Append("\":");
                    sb2.Append(SerializeObject(value, depth + 1, visited));
                }
                catch { }
            }
            sb2.Append("}");
            return sb2.ToString();
        }

        sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        static string GetRawValue(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return null;

            var searchKey = $"\"{key}\"";
            var keyIndex = json.IndexOf(searchKey, StringComparison.Ordinal);
            if (keyIndex < 0) return null;

            var colonIndex = json.IndexOf(':', keyIndex + searchKey.Length);
            if (colonIndex < 0) return null;

            var valueStart = colonIndex + 1;
            while (valueStart < json.Length && json[valueStart] == ' ') valueStart++;
            if (valueStart >= json.Length) return null;

            if (json[valueStart] == '"')
            {
                var end = FindStringEnd(json, valueStart);
                if (end < 0) return null;
                return json.Substring(valueStart + 1, end - valueStart - 1);
            }
            else if (json[valueStart] == '{' || json[valueStart] == '[')
            {
                var end = FindMatchingBrace(json, valueStart);
                if (end < 0) return null;
                return json.Substring(valueStart, end - valueStart + 1);
            }
            else
            {
                var end = valueStart;
                while (end < json.Length && json[end] != ',' && json[end] != '}' && json[end] != ']')
                    end++;
                return json.Substring(valueStart, end - valueStart).Trim();
            }
        }

        static int FindStringEnd(string json, int start)
        {
            var i = start + 1;
            while (i < json.Length)
            {
                if (json[i] == '\\') { i += 2; continue; }
                if (json[i] == '"') return i;
                i++;
            }
            return -1;
        }

        static int FindMatchingBrace(string json, int start)
        {
            var open = json[start];
            var close = open == '{' ? '}' : ']';
            var depth = 1;
            var i = start + 1;
            var inString = false;

            while (i < json.Length && depth > 0)
            {
                if (json[i] == '"') inString = !inString;
                else if (!inString)
                {
                    if (json[i] == open) depth++;
                    else if (json[i] == close) depth--;
                }
                i++;
            }
            return depth == 0 ? i - 1 : -1;
        }

        static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        static string UnescapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t")
                    .Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        sealed class Parser
        {
            readonly string _json;
            int _index;

            public Parser(string json)
            {
                _json = json;
            }

            public object ParseValue()
            {
                SkipWhitespace();
                if (_index >= _json.Length) return null;

                switch (_json[_index])
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': return ReadLiteral("true", true);
                    case 'f': return ReadLiteral("false", false);
                    case 'n': return ReadLiteral("null", null);
                    default: return ParseNumber();
                }
            }

            Dictionary<string, JsonElement> ParseObject()
            {
                var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                _index++;
                SkipWhitespace();

                while (_index < _json.Length && _json[_index] != '}')
                {
                    var key = ParseString();
                    SkipWhitespace();
                    if (_index < _json.Length && _json[_index] == ':') _index++;
                    result[key] = new JsonElement(ParseValue());
                    SkipWhitespace();
                    if (_index < _json.Length && _json[_index] == ',')
                    {
                        _index++;
                        SkipWhitespace();
                    }
                }

                if (_index < _json.Length && _json[_index] == '}') _index++;
                return result;
            }

            List<JsonElement> ParseArray()
            {
                var result = new List<JsonElement>();
                _index++;
                SkipWhitespace();

                while (_index < _json.Length && _json[_index] != ']')
                {
                    result.Add(new JsonElement(ParseValue()));
                    SkipWhitespace();
                    if (_index < _json.Length && _json[_index] == ',')
                    {
                        _index++;
                        SkipWhitespace();
                    }
                }

                if (_index < _json.Length && _json[_index] == ']') _index++;
                return result;
            }

            string ParseString()
            {
                if (_index >= _json.Length || _json[_index] != '"') return string.Empty;
                _index++;
                var sb = new StringBuilder();

                while (_index < _json.Length)
                {
                    var c = _json[_index++];
                    if (c == '"') break;
                    if (c == '\\' && _index < _json.Length)
                    {
                        var escaped = _json[_index++];
                        switch (escaped)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (_index + 4 <= _json.Length && int.TryParse(_json.Substring(_index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                                {
                                    sb.Append((char)code);
                                    _index += 4;
                                }
                                break;
                            default: sb.Append(escaped); break;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }

                return sb.ToString();
            }

            object ParseNumber()
            {
                var start = _index;
                while (_index < _json.Length && ",}] \t\r\n".IndexOf(_json[_index]) < 0) _index++;
                var text = _json.Substring(start, _index - start);
                if (text.IndexOf('.') >= 0 || text.IndexOf('e') >= 0 || text.IndexOf('E') >= 0)
                {
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
                }
                else if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                {
                    return l >= int.MinValue && l <= int.MaxValue ? (object)(int)l : l;
                }

                return text;
            }

            object ReadLiteral(string literal, object value)
            {
                if (_index + literal.Length <= _json.Length && string.Compare(_json, _index, literal, 0, literal.Length, StringComparison.Ordinal) == 0)
                {
                    _index += literal.Length;
                    return value;
                }

                return null;
            }

            void SkipWhitespace()
            {
                while (_index < _json.Length && char.IsWhiteSpace(_json[_index])) _index++;
            }
        }
    }
}
#endif
