using System;
using System.Linq;
using PathPlusPlus.Core;
using Xunit;

namespace PathPlusPlus.Tests
{
    public class PathNormalizerTests
    {
        private static readonly string SystemRoot = Environment.GetEnvironmentVariable("SystemRoot");

        [Theory]
        [InlineData(@"C:\Tools", @"c:\tools")]
        [InlineData(@"C:\Tools", @"C:\Tools\")]
        [InlineData(@"C:\Tools", @"C:\Tools\\")]
        [InlineData(@"C:\Tools", "  C:\\Tools  ")]
        [InlineData(@"C:\Tools", "\"C:\\Tools\"")]
        [InlineData(@"C:\Tools", @"C:/Tools")]
        [InlineData(@"C:\Tools\bin", @"C:\Tools\sub\..\bin")]
        [InlineData(@"\\server\share\x", @"\\SERVER\share\x\")]
        public void Equivalent_entries_match(string a, string b)
        {
            Assert.True(PathNormalizer.SameEntry(a, b));
        }

        [Fact]
        public void Variables_are_expanded_for_comparison()
        {
            Assert.True(PathNormalizer.SameEntry("%SystemRoot%\\System32", SystemRoot + "\\system32\\"));
        }

        [Theory]
        [InlineData(@"C:\Tools", @"C:\Tools2")]
        [InlineData(@"C:\Tools", @"D:\Tools")]
        [InlineData(@"C:\", @"C:")]
        public void Different_entries_do_not_match(string a, string b)
        {
            Assert.False(PathNormalizer.SameEntry(a, b));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\"\"")]
        public void Empty_entries_have_no_key(string entry)
        {
            Assert.Null(PathNormalizer.Key(entry));
        }

        [Theory]
        [InlineData("C:\"", @"C:\")]        // Explorer passes "C:\" and the \" escapes the quote
        [InlineData("C:", @"C:\")]
        [InlineData("\"C:\\foo\"", @"C:\foo")]
        [InlineData(@"C:\foo\", @"C:\foo\")]
        [InlineData(@"  D:\a b  ", @"D:\a b")]
        public void SanitizeArgument_repairs_explorer_arguments(string input, string expected)
        {
            Assert.Equal(expected, PathNormalizer.SanitizeArgument(input));
        }

        [Theory]
        [InlineData(@"C:\foo\bar.txt", @"C:\foo")]
        [InlineData(@"C:\foo", @"C:\")]
        [InlineData(@"C:\", @"C:\")]
        [InlineData(@"C:\foo\", @"C:\")]
        [InlineData(@"\\server\share\dir\file.txt", @"\\server\share\dir")]
        public void ParentDirectory_keeps_roots(string input, string expected)
        {
            Assert.Equal(expected, PathNormalizer.ParentDirectory(input));
        }

        [Theory]
        [InlineData(@"C:\", @"C:\")]
        [InlineData(@"C:\a\\", @"C:\a")]
        [InlineData(@"\\s\share\", @"\\s\share")]
        public void TrimTrailingSeparators(string input, string expected)
        {
            Assert.Equal(expected, PathNormalizer.TrimTrailingSeparators(input));
        }
    }

    public class PathListTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("a")]
        [InlineData("a;b;;c;")]
        [InlineData(";%SystemRoot%\\system32; C:\\x ;")]
        public void Round_trips_exactly(string raw)
        {
            Assert.Equal(raw, new PathList(raw).ToString());
        }

        [Fact]
        public void Add_appends_without_creating_empty_entries()
        {
            var list = new PathList(@"C:\a;C:\b;");
            Assert.True(list.Add(@"C:\c", prepend: false));
            Assert.Equal(@"C:\a;C:\b;C:\c", list.ToString());
        }

        [Fact]
        public void Add_prepends()
        {
            var list = new PathList(@"C:\a;C:\b");
            Assert.True(list.Add(@"C:\c", prepend: true));
            Assert.Equal(@"C:\c;C:\a;C:\b", list.ToString());
        }

        [Fact]
        public void Add_to_empty_path()
        {
            var list = new PathList(string.Empty);
            Assert.True(list.Add(@"C:\c", prepend: false));
            Assert.Equal(@"C:\c", list.ToString());
        }

        [Fact]
        public void Add_skips_equivalent_entries()
        {
            var list = new PathList(@"C:\Tools\;C:\b");
            Assert.False(list.Add(@"c:\tools", prepend: false));
            Assert.Equal(@"C:\Tools\;C:\b", list.ToString());
        }

        [Fact]
        public void Remove_removes_all_variants_and_leaves_others_untouched()
        {
            var list = new PathList(@"%SystemRoot%\system32;C:\Tools;c:\tools\; C:\TOOLS ;%USERPROFILE%\bin");
            Assert.Equal(3, list.Remove(@"C:\Tools"));
            Assert.Equal(@"%SystemRoot%\system32;%USERPROFILE%\bin", list.ToString());
        }

        [Fact]
        public void Remove_middle_entry_creates_no_empty_segment()
        {
            var list = new PathList(@"C:\a;C:\b;C:\c");
            Assert.Equal(1, list.Remove(@"C:\b"));
            Assert.Equal(@"C:\a;C:\c", list.ToString());
        }

        [Fact]
        public void Remove_missing_entry_returns_zero()
        {
            var list = new PathList(@"C:\a");
            Assert.Equal(0, list.Remove(@"C:\b"));
            Assert.Equal(@"C:\a", list.ToString());
        }

        [Fact]
        public void Dedupe_keeps_first_occurrence_and_drops_empties()
        {
            var list = new PathList(@"C:\a;;C:\b;c:\A\;C:\b; ;C:\c;");
            Assert.Equal(5, list.RemoveDuplicatesAndEmpty());
            Assert.Equal(@"C:\a;C:\b;C:\c", list.ToString());
        }

        [Fact]
        public void CountOf_and_Entries()
        {
            var list = new PathList(@"C:\a;;c:\a\;C:\b");
            Assert.Equal(2, list.CountOf(@"C:\A"));
            Assert.Equal(3, list.Entries.Count());
        }
    }

    public class PathAnalyzerTests
    {
        [Fact]
        public void Flags_problems()
        {
            string[] segments = { @"C:\exists", "", @"C:\missing", @"c:\exists\", "relative\\dir", "%NO_SUCH_VAR_PPTEST%\\bin", @"C:\shared" };
            var infos = PathAnalyzer.Analyze(segments, new[] { @"C:\shared" }, dir => !dir.EndsWith("missing", StringComparison.OrdinalIgnoreCase));

            Assert.Equal(EntryStatus.Ok, infos[0].Status);
            Assert.Equal(EntryStatus.Empty, infos[1].Status);
            Assert.Equal(EntryStatus.Missing, infos[2].Status);
            Assert.Equal(EntryStatus.Duplicate, infos[3].Status);
            Assert.Equal(EntryStatus.NotAbsolute, infos[4].Status);
            Assert.Equal(EntryStatus.UnresolvedVariable, infos[5].Status);
            Assert.Equal(EntryStatus.AlsoInOtherScope, infos[6].Status);
            Assert.Equal("Also in system PATH", infos[6].Describe(EnvScope.User));
        }
    }

    public class ValidationTests
    {
        [Theory]
        [InlineData(@"C:\Tools")]
        [InlineData(@"\\server\share\bin")]
        [InlineData(@"%USERPROFILE%\bin")]
        [InlineData(@"%NOT_DEFINED_YET_PPTEST%\bin")]
        public void Valid_path_entries(string entry)
        {
            Assert.Null(EnvValidation.ValidatePathEntry(entry));
        }

        [Theory]
        [InlineData("")]
        [InlineData(@"C:\a;C:\b")]
        [InlineData(@"relative\dir")]
        [InlineData("C:")]
        [InlineData("C:\\a\"b")]
        public void Invalid_path_entries(string entry)
        {
            Assert.NotNull(EnvValidation.ValidatePathEntry(entry));
        }

        [Theory]
        [InlineData("JAVA_HOME", true)]
        [InlineData("My Var", true)]
        [InlineData("", false)]
        [InlineData(" X", false)]
        [InlineData("A=B", false)]
        public void Names(string name, bool valid)
        {
            Assert.Equal(valid, EnvValidation.ValidateName(name) == null);
        }

        [Fact]
        public void Name_length_limit()
        {
            Assert.Null(EnvValidation.ValidateName(new string('A', 255)));
            Assert.NotNull(EnvValidation.ValidateName(new string('A', 256)));
        }
    }
}
