using System;
using System.Collections.Generic;
using System.Text;
using ONI_MP.Misc;
using ONI_MP.Networking;
using ONI_MP.Networking.Components;
using UnityEngine;

namespace ONI_MP.DebugTools.UnitTests
{
    public static class UITests
    {
        [UnitTest(category: "UI")]
        public static UnitTestResult ChatWindowExistsAndActive()
        {
            GameObject chatScreen = GameObject.Find("ChatScreen");
            if(chatScreen == null)
                return UnitTestResult.Fail("ChatScreen object not found in scene");

            bool isActive = chatScreen.activeSelf;
            if (!isActive)
                return UnitTestResult.Fail("ChatScreen object is not active");
            return UnitTestResult.Pass("ChatScreen object exists and is active");
        }

        [UnitTest(category: "UI")]
        public static UnitTestResult PingAndTrailSystemInitialized()
        {
            if (PingManager.Instance == null)
                return UnitTestResult.Fail("PingManager instance is null");
            return UnitTestResult.Pass("PingManager instance exists");
        }

        [UnitTest(category: "UI")]
        public static UnitTestResult NoGhostCursorsPresent()
        {
            if (!MultiplayerSession.IsHost && !MultiplayerSession.IsClient)
                return UnitTestResult.Fail("Not connected to a multiplayer session");

            // We - 1 to factor in our local cursor and client
            var clients = NetworkConfig.GetConnectedClients().Count - 1;
            var cursors = MultiplayerSession.PlayerCursors.Count - 1;

            if(cursors > clients)
                return UnitTestResult.Fail($"Number of player cursors ({cursors}) exceeds number of clients ({clients})");

            if (clients != cursors)
                return UnitTestResult.Fail($"Number of player cursors ({cursors}) does not match number of clients ({clients})");

            bool cursorSyncRunning = CursorManager.Instance != null && Utils.IsInGame() && MultiplayerSession.InSession && MultiplayerSession.LocalUserID.IsValid();
            if(!cursorSyncRunning)
                return UnitTestResult.Fail("Cursor synchronization does not appear to be running (CursorManager instance missing or not in game session)");

            return UnitTestResult.Pass("Number of player cursors matches number of clients");
        }
    }
}
