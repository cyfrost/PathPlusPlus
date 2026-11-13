using PathPlusPlus.Core;
using Xunit;

namespace PathPlusPlus.Tests
{
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
