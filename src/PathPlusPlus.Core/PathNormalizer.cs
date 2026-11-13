using System;
using System.IO;
using System.Text.RegularExpressions;

namespace PathPlusPlus.Core
{
    /// <summary>
    /// Turns PATH entries into comparison keys so that <c>C:\Tools</c>, <c>c:\tools\</c>,
    /// <c>"C:\Tools"</c> and <c>%SystemDrive%\Tools</c> are all recognised as the same folder.
    /// The original text of an entry is never modified — keys are only used for comparison.
    /// </summary>
    public static class PathNormalizer
    {
        private static readonly Regex UnresolvedVariable = new Regex("%[^%;]+%", RegexOptions.CultureInvariant);
        private static readonly Regex DriveOnly = new Regex("^[A-Za-z]:$", RegexOptions.CultureInvariant);

        /// <summary>Trims whitespace and one pair of surrounding double quotes.</summary>
        public static string Clean(string entry)
        {
            if (entry == null) return string.Empty;
            string s = entry.Trim();
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
            {
                s = s.Substring(1, s.Length - 2).Trim();
            }
            return s;
        }

        /// <summary>Expands %VARIABLES% using the current process environment.</summary>
        public static string Expand(string entry) => Environment.ExpandEnvironmentVariables(Clean(entry));

        /// <summary>True when the expanded entry still contains a %NAME% that could not be resolved.</summary>
        public static bool HasUnresolvedVariable(string entry) => UnresolvedVariable.IsMatch(Expand(entry));

        /// <summary>True for <c>C:\...</c> and <c>\\server\share...</c> paths.</summary>
        public static bool IsFullyQualified(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/')) return true;
            return path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns a case-insensitive comparison key for a PATH entry, or null for an empty entry.
        /// </summary>
        public static string Key(string entry)
        {
            string s = Clean(entry);
            if (s.Length == 0) return null;

            s = Environment.ExpandEnvironmentVariables(s).Replace('/', '\\');
            if (IsFullyQualified(s))
            {
                try
                {
                    s = Path.GetFullPath(s);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException || ex is IOException || ex is System.Security.SecurityException)
                {
                    // Keep the expanded text; it still makes a usable key.
                }
            }

            s = TrimTrailingSeparators(s);
            return s.ToUpperInvariant();
        }

        /// <summary>True when two entries refer to the same folder.</summary>
        public static bool SameEntry(string a, string b)
        {
            string ka = Key(a);
            return ka != null && string.Equals(ka, Key(b), StringComparison.Ordinal);
        }

        /// <summary>Removes trailing backslashes, except from a drive root such as <c>C:\</c>.</summary>
        public static string TrimTrailingSeparators(string path)
        {
            if (path == null) return null;
            string s = path;
            while (s.Length > 0 && s[s.Length - 1] == '\\' && !(s.Length == 3 && s[1] == ':'))
            {
                s = s.Substring(0, s.Length - 1);
            }
            return s;
        }

        /// <summary>
        /// Repairs a folder path received on a command line from Explorer.
        /// For a drive root, Explorer's <c>"%V"</c> becomes <c>"C:\"</c>, and the <c>\"</c>
        /// is parsed as an escaped quote, so the argument arrives as <c>C:"</c>.
        /// </summary>
        public static string SanitizeArgument(string argument)
        {
            if (argument == null) return null;
            string s = argument.Trim().Trim('"').Trim();
            if (DriveOnly.IsMatch(s)) s += "\\";
            return s;
        }

        /// <summary>The folder containing <paramref name="path"/>, or the path itself for a root.</summary>
        public static string ParentDirectory(string path)
        {
            string trimmed = TrimTrailingSeparators(path);
            string parent = Path.GetDirectoryName(trimmed);
            return string.IsNullOrEmpty(parent) ? trimmed : parent;
        }
    }
}
