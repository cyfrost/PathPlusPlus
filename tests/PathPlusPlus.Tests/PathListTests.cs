using System.Linq;
using PathPlusPlus.Core;
using Xunit;

namespace PathPlusPlus.Tests
{
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
}
