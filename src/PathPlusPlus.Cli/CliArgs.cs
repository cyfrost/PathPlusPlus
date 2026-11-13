using System;
using System.Collections.Generic;

namespace PathPlusPlus.Cli
{
    internal sealed class CliUsageException : Exception
    {
        public CliUsageException(string message) : base(message)
        {
        }
    }

    /// <summary>Minimal, strict argument parser: "command [positionals] [--flags]".</summary>
    internal sealed class CliArgs
    {
        private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["--user"] = "--user",
            ["-u"] = "--user",
            ["--machine"] = "--machine",
            ["--system"] = "--machine",
            ["-m"] = "--machine",
            ["--all"] = "--all",
            ["-a"] = "--all",
            ["--prepend"] = "--prepend",
            ["-p"] = "--prepend",
            ["--force"] = "--force",
            ["-f"] = "--force",
            ["--plain"] = "--plain",
            ["--help"] = "--help",
            ["-h"] = "--help",
            ["-?"] = "--help",
            ["/?"] = "--help",
            ["--version"] = "--version",
        };

        public string Command { get; private set; }
        public List<string> Positionals { get; } = new List<string>();
        public HashSet<string> Flags { get; } = new HashSet<string>(StringComparer.Ordinal);

        public bool Has(string flag) => Flags.Contains(flag);

        public static CliArgs Parse(string[] args)
        {
            var result = new CliArgs();
            bool optionsEnded = false;
            foreach (string arg in args)
            {
                if (!optionsEnded && arg == "--")
                {
                    optionsEnded = true;
                    continue;
                }

                bool looksLikeFlag = !optionsEnded && arg.Length > 1 && (arg[0] == '-' || arg == "/?");
                if (looksLikeFlag)
                {
                    if (!Aliases.TryGetValue(arg, out string canonical))
                    {
                        throw new CliUsageException("Unknown option '" + arg + "'.");
                    }
                    result.Flags.Add(canonical);
                }
                else if (result.Command == null)
                {
                    result.Command = arg.ToLowerInvariant();
                }
                else
                {
                    result.Positionals.Add(arg);
                }
            }
            return result;
        }

        /// <summary>Rejects flags the command doesn't understand.</summary>
        public void AllowOnly(params string[] allowed)
        {
            var ok = new HashSet<string>(allowed, StringComparer.Ordinal) { "--help" };
            foreach (string flag in Flags)
            {
                if (!ok.Contains(flag)) throw new CliUsageException("Option '" + flag + "' can't be used with '" + Command + "'.");
            }
        }

        public void ExpectPositionals(int count, string usage)
        {
            if (Positionals.Count != count) throw new CliUsageException("Usage: pathpp " + usage);
        }
    }
}
