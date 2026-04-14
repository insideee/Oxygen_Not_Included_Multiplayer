using HarmonyLib;
using ONI_MP.Networking;
using Shared.Profiling;

namespace ONI_MP.Patches.World
{
	[HarmonyPatch(typeof(BatteryTracker), "UpdateData")]
	public static class BatteryTrackerPatch
	{
		private sealed class ClientRefreshScope : System.IDisposable
		{
			public void Dispose()
			{
				_allowedClientRefreshDepth = System.Math.Max(0, _allowedClientRefreshDepth - 1);
			}
		}

		private static int _allowedClientRefreshDepth;

		internal static System.IDisposable AllowClientRefresh()
		{
			_allowedClientRefreshDepth++;
			return new ClientRefreshScope();
		}

		public static bool Prefix(BatteryTracker __instance)
		{
			using var _ = Profiler.Scope();

			if (GameClient.IsHardSyncInProgress)
				return false;

			if (!MultiplayerSession.InSession)
				return true;

			return MultiplayerSession.IsHost || _allowedClientRefreshDepth > 0;
		}
	}
}
