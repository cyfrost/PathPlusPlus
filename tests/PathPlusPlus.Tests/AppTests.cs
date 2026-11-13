using System;
using PathPlusPlus.App;
using Xunit;

namespace PathPlusPlus.Tests
{
    public class AppTests
    {
        // The argv values below are exactly what Windows hands the app for each Explorer verb
        // (note: "%V" on a drive root arrives as  C:"  because \" is parsed as an escaped quote).
        [Theory]
        [InlineData(new[] { "--add-to-path", "C:\"" }, @"C:\")]
        [InlineData(new[] { "--add-to-path", @"C:\Tools\bin\" }, @"C:\Tools\bin")]
        [InlineData(new[] { "--add-to-path", "--parent", @"C:\Windows\notepad.exe" }, @"C:\Windows")]
        [InlineData(new[] { "--remove-from-path", "--parent", @"C:\file.txt" }, @"C:\")]
        [InlineData(new[] { "--add-to-path", @"C:\Tools\app.exe" }, @"C:\Tools")]           // a file without --parent
        [InlineData(new[] { "--new-variable", @"C:\Tools\app.exe" }, @"C:\Tools\app.exe")] // variables keep the file path
        [InlineData(new[] { "--new-variable", "--parent", @"C:\Program Files\Git\bin\git.exe" }, @"C:\Program Files\Git\bin")]
        [InlineData(new[] { "--ADD-TO-PATH", @"\\server\share\tools" }, @"\\server\share\tools")]
        public void Explorer_invocations_resolve_to_the_right_path(string[] args, string expected)
        {
            string error = PathPlusPlus.App.Program.ResolveTarget(args, p => p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".txt", StringComparison.OrdinalIgnoreCase), out string target);
            Assert.Null(error);
            Assert.Equal(expected, target);
        }

        [Theory]
        [InlineData("--add-to-path")]
        [InlineData("--add-to-path|a|b")]
        [InlineData("--frob|C:\\x")]
        [InlineData("--add-to-path|\"\"")]
        public void Bad_invocations_are_rejected(string joinedArgs)
        {
            Assert.NotNull(PathPlusPlus.App.Program.ResolveTarget(joinedArgs.Split('|'), p => false, out _));
        }

        [Theory]
        [InlineData(@"C:\Tools\jdk-21", "JDK_21")]
        [InlineData(@"C:\Tools\go\", "GO")]
        [InlineData(@"C:\7zip", "_7ZIP")]
        [InlineData(@"C:\", "")]
        public void Suggested_variable_names(string path, string expected)
        {
            Assert.Equal(expected, NewVariableDialog.SuggestName(path));
        }
    }
}
