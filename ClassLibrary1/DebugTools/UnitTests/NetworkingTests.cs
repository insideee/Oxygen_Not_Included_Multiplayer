using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ONI_MP.Networking;
using static UnityEngine.LowLevelPhysics2D.PhysicsQuery;

namespace ONI_MP.DebugTools.UnitTests
{
    public static class NetworkingTests
    {
        [UnitTest(name: "Server is running", category: "Networking")]
        public static UnitTestResult ServerStarts()
        {
            if (NetworkConfig.TransportServer == null)
                return UnitTestResult.Fail("TransportServer is null");

            if (!MultiplayerSession.IsHost && !MultiplayerSession.IsClient)
                return UnitTestResult.Fail("Server not running yet");

            return UnitTestResult.Pass("Server is running");
        }

        [UnitTest(name: "Using Steamworks Transport", category: "Networking")]
        public static UnitTestResult IsSteamTransport()
        {
            if (NetworkConfig.transport != NetworkConfig.NetworkTransport.STEAMWORKS)
                return UnitTestResult.Fail("Transport is not Steamworks");
            return UnitTestResult.Pass("Transport is Steamworks");
        }

        [UnitTest(name: "Using Riptide Transport", category: "Networking")]
        public static UnitTestResult IsRiptideTransport()
        {
            if (NetworkConfig.transport != NetworkConfig.NetworkTransport.RIPTIDE)
                return UnitTestResult.Fail("Transport is not Riptide");
            return UnitTestResult.Pass("Transport is Riptide");
        }

        [UnitTest(name: "Check for duplicate network identities", category: "Networking")]
        public static UnitTestResult CheckForDuplicateNetworkIdentities()
        {
            var identities = NetworkIdentityRegistry.AllIdentities;
            foreach(var identity in identities)
            {
                int id = identity.NetId;
                var matches = identities.Where(x => x.NetId == id).ToList();
                if (matches.Count > 1)
                    return UnitTestResult.Fail($"NetId {identity.NetId} has {matches.Count} identities");
            }
            return UnitTestResult.Pass("No duplicate network identities found");
        }

    }
}
