using System;
using System.Collections.Generic;

namespace PathPlusPlus.Core
{
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
}
