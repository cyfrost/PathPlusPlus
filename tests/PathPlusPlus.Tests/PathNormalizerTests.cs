using System;
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
}
