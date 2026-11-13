using System;
using System.Collections.Generic;
using System.Text;

namespace PathPlusPlus.Core
{
    /// <summary>"key=base64(value)" lines; a missing key means null.</summary>
    internal sealed class FieldWriter
    {
        private readonly StringBuilder _sb = new StringBuilder();

        public void Add(string key, string value)
        {
            if (value == null) return;
            _sb.Append(key).Append('=').Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(value))).Append('\n');
        }

        public override string ToString() => _sb.ToString();

        public static Dictionary<string, string> Parse(string text)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in (text ?? string.Empty).Split('\n'))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                result[line.Substring(0, eq)] = Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(eq + 1).Trim()));
            }
            return result;
        }

        public static string Required(Dictionary<string, string> f, string key)
        {
            if (!f.TryGetValue(key, out string v)) throw new FormatException("Missing field '" + key + "'.");
            return v;
        }

        public static string Optional(Dictionary<string, string> f, string key) => f.TryGetValue(key, out string v) ? v : null;
    }
}
