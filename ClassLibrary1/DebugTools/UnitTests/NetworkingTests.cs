using System;
using System.Collections.Generic;
using System.Text;
using ONI_MP.Networking;
using ONI_MP.Tests;
using static UnityEngine.LowLevelPhysics2D.PhysicsQuery;

namespace ONI_MP.DebugTools.UnitTests
{
    public static class NetworkingTests
    {
        [UnitTest(category: "Networking")]
        public static UnitTestResult ServerStarts()
        {
            if (NetworkConfig.TransportServer == null)
                return UnitTestResult.Fail("TransportServer is null");

            if (!MultiplayerSession.IsHost || !MultiplayerSession.IsClient)
                return UnitTestResult.Fail("Server not running yet");

            return UnitTestResult.Pass("Server is running");
        }

        [UnitTest(category: "Networking")]
        public static UnitTestResult IsSteamTransport()
        {
            if (NetworkConfig.transport != NetworkConfig.NetworkTransport.STEAMWORKS)
                return UnitTestResult.Fail("Transport is not Steamworks");
            return UnitTestResult.Pass("Transport is Steamworks");
        }

        [UnitTest(category: "Networking")]
        public static UnitTestResult IsRiptideTransport()
        {
            if (NetworkConfig.transport != NetworkConfig.NetworkTransport.RIPTIDE)
                return UnitTestResult.Fail("Transport is not Riptide");
            return UnitTestResult.Pass("Transport is Riptide");
        }

    }
}
