using ONI_MP.Networking.Packets.Architecture;
using Shared.Profiling;

namespace ONI_MP.Networking
{
	internal static class ProtocolCompatibility
	{
		public const int CurrentProtocolVersion = 1;

		private static int? _packetFingerprint;
		private static string _modVersion;

		public static int PacketFingerprint
		{
			get
			{
				using var _ = Profiler.Scope();

				return _packetFingerprint ??= PacketRegistry.GetRegisteredPacketFingerprint();
			}
		}

		public static string ModVersion
		{
			get
			{
				using var _ = Profiler.Scope();

				return _modVersion ??= ONI_MP.ModUpdater.Updater.GetVersion();
			}
		}

		public static bool Matches(int protocolVersion, int packetFingerprint)
		{
			using var _ = Profiler.Scope();

			return protocolVersion == CurrentProtocolVersion
				&& packetFingerprint == PacketFingerprint;
		}

		public static string BuildMismatchReason(int remoteProtocolVersion, int remotePacketFingerprint, string remoteModVersion, bool hasMetadata)
		{
			using var _ = Profiler.Scope();

			if (!hasMetadata)
			{
				return "Peer is running a build without protocol metadata.";
			}

			if (remoteProtocolVersion != CurrentProtocolVersion)
			{
				return $"Protocol mismatch. Host={CurrentProtocolVersion}, Peer={remoteProtocolVersion}.";
			}

			if (remotePacketFingerprint != PacketFingerprint)
			{
				return $"Packet registry mismatch. Host={PacketFingerprint}, Peer={remotePacketFingerprint}.";
			}

			if (!string.IsNullOrEmpty(remoteModVersion) && remoteModVersion != ModVersion)
			{
				return $"Mod version mismatch. Host={ModVersion}, Peer={remoteModVersion}.";
			}

			return "Peer is running an incompatible build.";
		}
	}
}
