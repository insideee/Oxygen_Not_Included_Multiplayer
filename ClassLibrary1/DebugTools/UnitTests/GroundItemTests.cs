using System.IO;
using ONI_MP.Networking;
using ONI_MP.Networking.Components;
using ONI_MP.Networking.Packets.World;

namespace ONI_MP.DebugTools.UnitTests
{
	public static class GroundItemTests
	{
		[UnitTest(name: "GroundItemPickedUpPacket: serialization roundtrip", category: "GroundItems")]
		public static UnitTestResult PacketRoundtrip()
		{
			var original = new GroundItemPickedUpPacket { NetId = 999888777 };
			using var ms = new MemoryStream();
			using var writer = new BinaryWriter(ms);
			original.Serialize(writer);
			ms.Position = 0;
			using var reader = new BinaryReader(ms);
			var copy = new GroundItemPickedUpPacket();
			copy.Deserialize(reader);
			if (copy.NetId != original.NetId)
				return UnitTestResult.Fail($"NetId mismatch: {copy.NetId} != {original.NetId}");
			return UnitTestResult.Pass("GroundItemPickedUpPacket roundtrip OK");
		}

		[UnitTest(name: "GroundItemPickedUpPacket: is IBulkablePacket", category: "GroundItems")]
		public static UnitTestResult IsBulkable()
		{
			var p = new GroundItemPickedUpPacket();
			if (p.MaxPackSize != 200)
				return UnitTestResult.Fail($"MaxPackSize expected 200, got {p.MaxPackSize}");
			if (p.IntervalMs != 200)
				return UnitTestResult.Fail($"IntervalMs expected 200, got {p.IntervalMs}");
			return UnitTestResult.Pass($"GroundItemPickedUpPacket IBulkablePacket: MaxPackSize={p.MaxPackSize}, IntervalMs={p.IntervalMs}");
		}

		[UnitTest(name: "GroundItems: NetworkIdentityRegistry accessible", category: "GroundItems")]
		public static UnitTestResult RegistryAccessible()
		{
			// TryGetComponent with a non-existent NetId should return false (not throw)
			bool found = NetworkIdentityRegistry.TryGetComponent<Pickupable>(-1, out _);
			if (found)
				return UnitTestResult.Fail("NetId -1 should not exist in registry");
			return UnitTestResult.Pass("NetworkIdentityRegistry.TryGetComponent accessible and returns false for unknown NetId");
		}

		[UnitTest(name: "ClearTool.Instance accessible (sweep relay)", category: "GroundItems")]
		public static UnitTestResult ClearToolAccessible()
		{
			if (ClearTool.Instance == null)
				return UnitTestResult.Fail("ClearTool.Instance is null");
			return UnitTestResult.Pass("ClearTool.Instance accessible");
		}
	}
}
