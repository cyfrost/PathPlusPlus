using System;
using System.Collections.Generic;
using System.Linq;

namespace PathPlusPlus.Core
{
    /// <summary>
    /// An editable PATH value. The value is split on ';' and each segment is kept exactly as written,
    /// so entries that are not touched survive a round trip byte-for-byte.
    /// </summary>
    public sealed class PathList
    {
        private readonly List<string> _segments;

        public PathList(string raw)
        {
            _segments = string.IsNullOrEmpty(raw) ? new List<string>() : raw.Split(';').ToList();
        }

        public PathList(IEnumerable<string> segments)
        {
            _segments = segments?.ToList() ?? new List<string>();
        }

        /// <summary>Every segment, including empty ones, in order.</summary>
        public IReadOnlyList<string> Segments => _segments;

        /// <summary>Non-empty entries, in order.</summary>
        public IEnumerable<string> Entries => _segments.Where(s => PathNormalizer.Clean(s).Length > 0);

        public int IndexOf(string entry)
        {
            string key = PathNormalizer.Key(entry);
            if (key == null) return -1;
            for (int i = 0; i < _segments.Count; i++)
            {
                if (string.Equals(PathNormalizer.Key(_segments[i]), key, StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        public bool Contains(string entry) => IndexOf(entry) >= 0;

        public int CountOf(string entry)
        {
            string key = PathNormalizer.Key(entry);
            if (key == null) return 0;
            return _segments.Count(s => string.Equals(PathNormalizer.Key(s), key, StringComparison.Ordinal));
        }

        /// <summary>Adds an entry unless an equivalent one is already present. Returns true when added.</summary>
        public bool Add(string entry, bool prepend)
        {
            if (PathNormalizer.Key(entry) == null) throw new ArgumentException("Entry is empty.", nameof(entry));
            if (Contains(entry)) return false;

            if (prepend)
            {
                _segments.Insert(0, entry);
            }
            else
            {
                // Drop trailing empty segments ("a;b;") so we don't create "a;b;;c".
                while (_segments.Count > 0 && PathNormalizer.Clean(_segments[_segments.Count - 1]).Length == 0)
                {
                    _segments.RemoveAt(_segments.Count - 1);
                }
                _segments.Add(entry);
            }
            return true;
        }

        /// <summary>Removes every entry equivalent to <paramref name="entry"/>. Returns the number removed.</summary>
        public int Remove(string entry)
        {
            string key = PathNormalizer.Key(entry);
            if (key == null) return 0;
            return _segments.RemoveAll(s => string.Equals(PathNormalizer.Key(s), key, StringComparison.Ordinal));
        }

        /// <summary>Removes empty segments and later duplicates of earlier entries. Returns the number removed.</summary>
        public int RemoveDuplicatesAndEmpty()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            return _segments.RemoveAll(s =>
            {
                string key = PathNormalizer.Key(s);
                return key == null || !seen.Add(key);
            });
        }

        public override string ToString() => string.Join(";", _segments);
    }
}
