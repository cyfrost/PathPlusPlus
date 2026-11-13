using System;
using System.Collections.Generic;
using System.Text;

namespace PathPlusPlus.Core
{
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
}
