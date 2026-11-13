using System.Collections.Generic;
using PathPlusPlus.Core;

namespace PathPlusPlus.Tests
{
    internal sealed class FakeElevator : IElevator
    {
        public List<EnvRequest> Requests { get; } = new List<EnvRequest>();
        public EnvResponse Response { get; set; } = new EnvResponse { Success = true, Changed = true, Message = "elevated ok" };

        public EnvResponse RunElevated(EnvRequest request)
        {
            Requests.Add(request);
            return Response;
        }
    }
}
