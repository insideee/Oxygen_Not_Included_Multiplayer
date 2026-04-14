namespace Shared.Interfaces.Networking
{
	/// <summary>
	/// Marks a packet that a client sends via SendToAllOtherPeers and expects
	/// the host to rebroadcast to other clients (command relay semantics).
	/// Without this marker, IBulkablePacket takes a direct path to the host
	/// that skips the HostBroadcastPacket wrapper and therefore the rebroadcast.
	/// </summary>
	public interface IClientRelayable
	{
	}
}
