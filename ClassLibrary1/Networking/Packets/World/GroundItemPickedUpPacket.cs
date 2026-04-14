using ONI_MP.DebugTools;
using ONI_MP.Networking.Packets.Architecture;
using Shared.Interfaces.Networking;
using System.IO;
using Shared.Profiling;

namespace ONI_MP.Networking.Packets.World
{
	/// <summary>
	/// Host -> clients: remove a tracked ground item by NetId.
	/// Batched via IBulkablePacket -- multiple pickups in 200ms window = one message.
	/// 4 bytes per item. WorldDamageSpawnResourcePacket already assigns matching NetIds
	/// via identity.OverrideNetId(NetId), so client registry lookup is reliable.
	/// </summary>
	public class GroundItemPickedUpPacket : IPacket, IBulkablePacket
	{
		public int NetId;

		public int MaxPackSize => 200;  // up to 200 items per bulk flush
		public uint IntervalMs => 200;  // BulkPacketMonitor flush interval (ms)

		public void Serialize(BinaryWriter writer)
		{
			using var _ = Profiler.Scope();
			writer.Write(NetId);
		}

		public void Deserialize(BinaryReader reader)
		{
			using var _ = Profiler.Scope();
			NetId = reader.ReadInt32();
		}

		public void OnDispatched()
		{
			using var _ = Profiler.Scope();

			if (!NetworkIdentityRegistry.TryGetComponent<Pickupable>(NetId, out var pickupable))
				return;

			Util.KDestroyGameObject(pickupable.gameObject);
		}
	}
}
