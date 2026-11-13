using System;

namespace PathPlusPlus.Core
{
    /// <summary>Input validation for variable names, values and PATH entries.</summary>
    public static class EnvValidation
    {
        public const string PathVariableName = "Path";

        /// <summary>Windows limits an environment variable name stored in the registry to 255 characters.</summary>
        public const int MaxNameLength = 255;

        /// <summary>Maximum length of a single environment variable value in a process environment block.</summary>
        public const int MaxValueLength = 32767;

        /// <summary>Some older programs truncate PATH beyond this length.</summary>
        public const int LegacyPathWarningLength = 2047;

        public static bool IsPathName(string name) =>
            string.Equals(name?.Trim(), PathVariableName, StringComparison.OrdinalIgnoreCase);

        /// <summary>Returns an error message, or null when the name is valid.</summary>
        public static string ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "The variable name is empty.";
            if (name != name.Trim()) return "The variable name must not start or end with spaces.";
            if (name.IndexOf('=') >= 0) return "The variable name must not contain '='.";
            if (name.IndexOf('\0') >= 0) return "The variable name contains an invalid character.";
            if (name.Length > MaxNameLength) return "The variable name is longer than " + MaxNameLength + " characters.";
            return null;
        }

        /// <summary>Returns an error message, or null when the value is valid.</summary>
        public static string ValidateValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return "The value is empty. To delete a variable, remove it instead.";
            if (value.IndexOf('\0') >= 0) return "The value contains an invalid character.";
            if (value.Length > MaxValueLength) return "The value is longer than " + MaxValueLength + " characters.";
            return null;
        }

        /// <summary>Returns an error message, or null when <paramref name="entry"/> can be added to PATH.</summary>
        public static string ValidatePathEntry(string entry)
        {
            string cleaned = PathNormalizer.Clean(entry);
            if (cleaned.Length == 0) return "The folder is empty.";
            if (cleaned.IndexOf(';') >= 0) return "The folder contains ';', which would split it into several PATH entries.";
            if (cleaned.IndexOf('"') >= 0) return "The folder must not contain quotes.";
            if (cleaned.IndexOf('\0') >= 0) return "The folder contains an invalid character.";

            // A %VARIABLE% that this process can't resolve (e.g. one defined moments ago) is allowed;
            // Windows resolves it when the PATH is loaded.
            if (PathNormalizer.HasUnresolvedVariable(cleaned)) return null;

            if (!PathNormalizer.IsFullyQualified(PathNormalizer.Expand(cleaned)))
            {
                return "The folder must be a full path such as C:\\Tools\\bin.";
            }
            return null;
        }
    }
}
