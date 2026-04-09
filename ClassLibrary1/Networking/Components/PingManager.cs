using ONI_MP.Misc;
using ONI_MP.Networking.Packets.Social;
using Shared.Profiling;
using Steamworks;
using UnityEngine;

namespace ONI_MP.Networking.Components
{
	public class PingManager : MonoBehaviour
	{
		public static PingManager Instance { get; private set; }

		private const float CooldownSeconds = 1f;
		private float lastPingTime;

		private void Awake()
		{
			using var _ = Profiler.Scope();

			if (Instance != null)
			{
				Destroy(this);
				return;
			}

			Instance = this;
			DontDestroyOnLoad(gameObject);
		}

		private void Update()
		{
			using var _ = Profiler.Scope();

			if (!Utils.IsInGame())
				return;

			if (!MultiplayerSession.InSession || !MultiplayerSession.LocalUserID.IsValid())
				return;

			if (Input.GetKey(KeyCode.LeftControl) && Input.GetMouseButtonDown(2) && Time.unscaledTime - lastPingTime >= CooldownSeconds)
			{
				SendPing();
				lastPingTime = Time.unscaledTime;
			}
		}

		private void SendPing()
		{
			using var _ = Profiler.Scope();

			Vector3 worldPos = GetWorldMousePosition();
			var packet = new PingPacket(worldPos);

			ShowPing(MultiplayerSession.LocalUserID, worldPos, CursorManager.Instance.color);

			if (MultiplayerSession.IsHost)
				PacketSender.SendToAllClients(packet);
			else
				PacketSender.SendToHost(packet);
		}

		public void ShowPing(ulong playerID, Vector3 worldPos, Color color)
		{
			using var _ = Profiler.Scope();

			var canvasGO = GameScreenManager.Instance.ssCameraCanvas;
			if (canvasGO == null)
				return;

			string playerName = ResolvePlayerName(playerID);

			var markerGO = new GameObject($"PingMarker_{playerID}");
			markerGO.transform.SetParent(canvasGO.transform, false);
			markerGO.layer = LayerMask.NameToLayer("UI");

			var marker = markerGO.AddComponent<PingMarker>();
			marker.Init(worldPos, color, playerName);
		}

		private string ResolvePlayerName(ulong playerID)
		{
			using var _ = Profiler.Scope();

			if (NetworkConfig.IsSteamConfig() && SteamFriends.HasFriend(playerID.AsCSteamID(), EFriendFlags.k_EFriendFlagImmediate))
				return SteamFriends.GetFriendPersonaName(playerID.AsCSteamID());

			var player = MultiplayerSession.GetPlayer(playerID);
			if (player != null)
				return player.PlayerName;

			return $"Player {playerID}";
		}

		private Vector3 GetWorldMousePosition()
		{
			using var _ = Profiler.Scope();

			Vector3 mousePos = Input.mousePosition;
			mousePos.z = 0f;
			if (Camera.main != null)
				return Camera.main.ScreenToWorldPoint(mousePos);
			return Vector3.zero;
		}
	}
}
