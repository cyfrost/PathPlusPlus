using System;
using System.Collections.Generic;
using System.Text;

namespace PathPlusPlus.Core
{
    public enum EnvOperation
    {
        AddToPath,
        RemoveFromPath,
        DedupePath,
        SetPath,
        SetVariable,
        DeleteVariable,
        Restore,
    }

    /// <summary>
    /// A single environment change. Requests are serialised so a system-scope change can be handed to an
    /// elevated helper process without any command-line quoting problems.
    /// </summary>
    public sealed class EnvRequest
    {
        public EnvOperation Operation { get; set; }
        public EnvScope Scope { get; set; }
        public string Entry { get; set; }
        public bool Prepend { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Expected { get; set; }
        public bool Force { get; set; }
        public string BackupFile { get; set; }

        public static EnvRequest AddToPath(EnvScope scope, string entry, bool prepend) =>
            new EnvRequest { Operation = EnvOperation.AddToPath, Scope = scope, Entry = entry, Prepend = prepend };

        public static EnvRequest RemoveFromPath(EnvScope scope, string entry) =>
            new EnvRequest { Operation = EnvOperation.RemoveFromPath, Scope = scope, Entry = entry };

        public static EnvRequest DedupePath(EnvScope scope) =>
            new EnvRequest { Operation = EnvOperation.DedupePath, Scope = scope };

        public static EnvRequest SetPath(EnvScope scope, string value, string expected) =>
            new EnvRequest { Operation = EnvOperation.SetPath, Scope = scope, Value = value, Expected = expected };

        public static EnvRequest SetVariable(EnvScope scope, string name, string value, bool force) =>
            new EnvRequest { Operation = EnvOperation.SetVariable, Scope = scope, Name = name, Value = value, Force = force };

        public static EnvRequest DeleteVariable(EnvScope scope, string name, bool force) =>
            new EnvRequest { Operation = EnvOperation.DeleteVariable, Scope = scope, Name = name, Force = force };

        public static EnvRequest Restore(EnvScope scope, string backupFile) =>
            new EnvRequest { Operation = EnvOperation.Restore, Scope = scope, BackupFile = backupFile };

        public string Serialize()
        {
            var w = new FieldWriter();
            w.Add("op", Operation.ToString());
            w.Add("scope", Scope.ToString());
            w.Add("entry", Entry);
            w.Add("prepend", Prepend ? "1" : "0");
            w.Add("name", Name);
            w.Add("value", Value);
            w.Add("expected", Expected);
            w.Add("force", Force ? "1" : "0");
            w.Add("backup", BackupFile);
            return w.ToString();
        }

        public static EnvRequest Deserialize(string text)
        {
            Dictionary<string, string> f = FieldWriter.Parse(text);
            return new EnvRequest
            {
                Operation = (EnvOperation)Enum.Parse(typeof(EnvOperation), FieldWriter.Required(f, "op")),
                Scope = EnvScopeExtensions.Parse(FieldWriter.Required(f, "scope")),
                Entry = FieldWriter.Optional(f, "entry"),
                Prepend = FieldWriter.Optional(f, "prepend") == "1",
                Name = FieldWriter.Optional(f, "name"),
                Value = FieldWriter.Optional(f, "value"),
                Expected = FieldWriter.Optional(f, "expected"),
                Force = FieldWriter.Optional(f, "force") == "1",
                BackupFile = FieldWriter.Optional(f, "backup"),
            };
        }

        /// <summary>A command-line-safe token (Base64 only uses A–Z a–z 0–9 + / =).</summary>
        public string ToToken() => Convert.ToBase64String(Encoding.UTF8.GetBytes(Serialize()));

        public static EnvRequest FromToken(string token) => Deserialize(Encoding.UTF8.GetString(Convert.FromBase64String(token)));
    }

    /// <summary>The outcome of an <see cref="EnvRequest"/>.</summary>
    public sealed class EnvResponse
    {
        public bool Success { get; set; }
        public EnvError Error { get; set; }
        public bool Changed { get; set; }
        public int Count { get; set; }
        public string Message { get; set; }

        public static EnvResponse FromResult(ChangeResult r) =>
            new EnvResponse { Success = true, Error = EnvError.None, Changed = r.Changed, Count = r.Count, Message = r.Message };

        public static EnvResponse Fail(EnvError error, string message) =>
            new EnvResponse { Success = false, Error = error == EnvError.None ? EnvError.Failed : error, Message = message };

        public string Serialize()
        {
            var w = new FieldWriter();
            w.Add("success", Success ? "1" : "0");
            w.Add("error", Error.ToString());
            w.Add("changed", Changed ? "1" : "0");
            w.Add("count", Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            w.Add("message", Message);
            return w.ToString();
        }

        public static EnvResponse Deserialize(string text)
        {
            Dictionary<string, string> f = FieldWriter.Parse(text);
            return new EnvResponse
            {
                Success = FieldWriter.Required(f, "success") == "1",
                Error = (EnvError)Enum.Parse(typeof(EnvError), FieldWriter.Required(f, "error")),
                Changed = FieldWriter.Optional(f, "changed") == "1",
                Count = int.Parse(FieldWriter.Optional(f, "count") ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                Message = FieldWriter.Optional(f, "message"),
            };
        }
    }

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
