using System;
using PathPlusPlus.Core;
using Xunit;

namespace PathPlusPlus.Tests
{
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
}
