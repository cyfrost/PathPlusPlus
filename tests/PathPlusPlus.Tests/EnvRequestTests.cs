using PathPlusPlus.Core;
using Xunit;

namespace PathPlusPlus.Tests
{
    public class EnvRequestTests
    {
        [Fact]
        public void Request_round_trips_through_token()
        {
            var request = EnvRequest.SetPath(EnvScope.Machine, "C:\\a;\"quoted\";ünïcødé;\nnewline", "expected value");
            EnvRequest copy = EnvRequest.FromToken(request.ToToken());

            Assert.Equal(request.Operation, copy.Operation);
            Assert.Equal(request.Scope, copy.Scope);
            Assert.Equal(request.Value, copy.Value);
            Assert.Equal(request.Expected, copy.Expected);
            Assert.Null(copy.Entry);
        }

        [Fact]
        public void Token_is_command_line_safe()
        {
            string token = EnvRequest.AddToPath(EnvScope.Machine, @"C:\Program Files\My Tool", true).ToToken();
            Assert.DoesNotContain(" ", token);
            Assert.DoesNotContain("\"", token);
        }

        [Fact]
        public void Response_round_trip()
        {
            var response = EnvResponse.Fail(EnvError.Conflict, "changed: a=b\nsecond line");
            EnvResponse copy = EnvResponse.Deserialize(response.Serialize());
            Assert.False(copy.Success);
            Assert.Equal(EnvError.Conflict, copy.Error);
            Assert.Equal(response.Message, copy.Message);
        }
    }
}
