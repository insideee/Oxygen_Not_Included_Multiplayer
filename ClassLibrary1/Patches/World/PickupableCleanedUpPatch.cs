using HarmonyLib;
using ONI_MP.DebugTools;
using ONI_MP.Networking;
using ONI_MP.Networking.Components;
using ONI_MP.Networking.Packets.World;
using Shared.Profiling;

namespace ONI_MP.Patches.World
{
	[HarmonyPatch(typeof(Pickupable), "OnCleanedUp")]
	public static class PickupableCleanedUpPatch
	{
		public static void Postfix(Pickupable __instance)
		{
			using var _ = Profiler.Scope();
			try
			{
				if (!MultiplayerSession.IsHost || !MultiplayerSession.InSession)
					return;

				var identity = __instance.GetComponent<NetworkIdentity>();
				if (identity == null)
					return;

				PacketSender.SendToAllClients(new GroundItemPickedUpPacket { NetId = identity.NetId });
				DebugConsole.Log($"[PickupableCleanedUpPatch] Sent GroundItemPickedUpPacket NetId={identity.NetId}");
			}
			catch (System.Exception ex)
			{
				DebugConsole.LogError($"[PickupableCleanedUpPatch] Exception: {ex}");
			}
		}
	}
}
