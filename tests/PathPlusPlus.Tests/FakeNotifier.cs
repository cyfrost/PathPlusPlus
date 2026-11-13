using PathPlusPlus.Core;

namespace PathPlusPlus.Tests
{
    internal sealed class FakeNotifier : IChangeNotifier
    {
        public int Count { get; private set; }

        public void EnvironmentChanged() => Count++;
    }
}
