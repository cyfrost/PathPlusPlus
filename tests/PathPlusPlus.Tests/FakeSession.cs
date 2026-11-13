using PathPlusPlus.Core;

namespace PathPlusPlus.Tests
{
    internal sealed class FakeSession : ISessionInfo
    {
        public bool IsElevated { get; set; }
        public bool IsDifferentUserThanDesktop { get; set; }
        public string UserName { get; set; } = "tester";
    }
}
