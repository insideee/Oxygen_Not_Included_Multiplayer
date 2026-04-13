using System.Collections.Generic;
using ONI_MP.Networking.Packets.Animation;
using Shared.Profiling;
using Steamworks;
using UnityEngine;

namespace ONI_MP.Networking.Components
{
	internal class AnimSyncCoordinator : MonoBehaviour
	{
		private class SyncState
		{
			public bool HasObservedState;
			public int LastActivityKey;
			public float LastChangedTime;
			public float LastSentTime;
		}

		private const float TickInterval = 0.2f;
		private const int ShardCount = 5;
		private const float RecentActivityWindow = 3f;
		private const float VisibleSyncInterval = 5f;
		private const float ActiveSyncInterval = 10f;
		private const int VisibilityMargin = 2;
		private const int PendingUnreliableBackoffBytes = 32768;
		private const long QueueTimeBackoffUsec = 100000;

		private static readonly HashSet<AnimStateSyncer> TrackedSyncers = [];

		public static AnimSyncCoordinator Instance { get; private set; }

		private readonly Dictionary<AnimStateSyncer, SyncState> _syncStates = [];
		private readonly Dictionary<ulong, HashSet<int>> _pendingResyncRequests = [];
		private readonly HashSet<ulong> _visibleRecipients = [];
		private float _tickTimer;
		private int _currentShard;

		private void Awake()
		{
			using var _ = Profiler.Scope();

			Instance = this;
		}

		private void OnDestroy()
		{
			using var _ = Profiler.Scope();

			if (Instance == this)
				Instance = null;
		}

		public static void Register(AnimStateSyncer syncer)
		{
			using var _ = Profiler.Scope();

			if (syncer != null)
				TrackedSyncers.Add(syncer);
		}

		public static void Unregister(AnimStateSyncer syncer)
		{
			using var _ = Profiler.Scope();

			if (syncer != null)
			{
				TrackedSyncers.Remove(syncer);
				Instance?._syncStates.Remove(syncer);
			}
		}

		public static List<AnimStateSyncer> GetTrackedSyncers()
		{
			using var _ = Profiler.Scope();

			var syncers = new List<AnimStateSyncer>(TrackedSyncers.Count);
			foreach (var syncer in TrackedSyncers)
			{
				if (syncer != null)
					syncers.Add(syncer);
			}
			return syncers;
		}

		public void QueueResyncRequest(ulong requesterId, IEnumerable<int> netIds)
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.IsHost || !MultiplayerSession.InSession)
				return;

			if (!MultiplayerSession.ConnectedPlayers.TryGetValue(requesterId, out var player) || player.Connection == null)
				return;

			if (!_pendingResyncRequests.TryGetValue(requesterId, out var requestSet))
			{
				requestSet = [];
				_pendingResyncRequests[requesterId] = requestSet;
			}

			foreach (var netId in netIds)
			{
				if (netId != 0)
					requestSet.Add(netId);
			}
		}

		private void Update()
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.InSession || !MultiplayerSession.IsHost)
				return;

			if (MultiplayerSession.ConnectedPlayers.Count == 0)
				return;

			_tickTimer += Time.unscaledDeltaTime;
			while (_tickTimer >= TickInterval)
			{
				_tickTimer -= TickInterval;
				RunTick();
			}
		}

		private void RunTick()
		{
			using var _ = Profiler.Scope();

			ProcessPendingRequests();

			var trackedSyncers = GetTrackedSyncers();
			if (trackedSyncers.Count == 0)
				return;

			bool applyBackoff = ShouldBackOffForSteamQueue();

			// Spread the full scan across five 200ms ticks to avoid bursty correction traffic.
			for (int i = 0; i < trackedSyncers.Count; i++)
			{
				if (i % ShardCount != _currentShard)
					continue;

				ProcessSyncer(trackedSyncers[i], applyBackoff);
			}

			_currentShard = (_currentShard + 1) % ShardCount;
		}

		private void ProcessPendingRequests()
		{
			using var _ = Profiler.Scope();

			if (_pendingResyncRequests.Count == 0)
				return;

			float now = Time.unscaledTime;
			var completed = new List<ulong>();

			foreach (var kvp in _pendingResyncRequests)
			{
				if (!MultiplayerSession.ConnectedPlayers.TryGetValue(kvp.Key, out var player) || player.Connection == null)
				{
					completed.Add(kvp.Key);
					continue;
				}

				foreach (var netId in kvp.Value)
				{
					if (!NetworkIdentityRegistry.TryGet(netId, out var identity))
						continue;
					if (!identity.TryGetComponent<AnimStateSyncer>(out var syncer))
						continue;
					if (!syncer.TryBuildSnapshot(out var packet, out var activityKey))
						continue;

					PacketSender.SendToPlayer(kvp.Key, packet, PacketSendMode.Unreliable);
					UpdateObservedState(syncer, activityKey, now);
					_syncStates[syncer].LastSentTime = now;
				}

				completed.Add(kvp.Key);
			}

			foreach (var requesterId in completed)
				_pendingResyncRequests.Remove(requesterId);
		}

		private void ProcessSyncer(AnimStateSyncer syncer, bool applyBackoff)
		{
			using var _ = Profiler.Scope();

			if (syncer == null)
				return;

			if (!syncer.TryBuildSnapshot(out var packet, out var activityKey))
				return;

			float now = Time.unscaledTime;
			bool activityChanged = UpdateObservedState(syncer, activityKey, now);
			bool visible = TryCollectVisibleRecipients(syncer);

			if (activityChanged && visible)
			{
				SendSnapshotToVisibleClients(packet, syncer, now);
				return;
			}

			var syncState = _syncStates[syncer];
			bool recentlyActive = now - syncState.LastChangedTime <= RecentActivityWindow;
			if (!visible && !recentlyActive)
				return;

			float interval = visible ? VisibleSyncInterval : ActiveSyncInterval;
			if (applyBackoff)
				interval *= 2f;

			if (now - syncState.LastSentTime < interval)
				return;

			if (visible)
				SendSnapshotToVisibleClients(packet, syncer, now);
			else
				SendSnapshotToAllClients(packet, syncer, now);
		}

		private bool UpdateObservedState(AnimStateSyncer syncer, int activityKey, float now)
		{
			using var _ = Profiler.Scope();

			if (!_syncStates.TryGetValue(syncer, out var syncState))
			{
				syncState = new SyncState();
				_syncStates[syncer] = syncState;
			}

			bool activityChanged = !syncState.HasObservedState || syncState.LastActivityKey != activityKey;
			if (activityChanged)
			{
				syncState.HasObservedState = true;
				syncState.LastActivityKey = activityKey;
				syncState.LastChangedTime = now;
			}

			return activityChanged;
		}

		private bool TryCollectVisibleRecipients(AnimStateSyncer syncer)
		{
			using var _ = Profiler.Scope();

			_visibleRecipients.Clear();
			if (WorldStateSyncer.Instance == null)
				return false;

			WorldStateSyncer.Instance.GetClientsViewingCell(syncer.GetGridCell(), _visibleRecipients, VisibilityMargin);
			return _visibleRecipients.Count > 0;
		}

		private void SendSnapshotToVisibleClients(AnimSyncPacket packet, AnimStateSyncer syncer, float now)
		{
			using var _ = Profiler.Scope();

			foreach (var recipient in _visibleRecipients)
				PacketSender.SendToPlayer(recipient, packet, PacketSendMode.Unreliable);

			_syncStates[syncer].LastSentTime = now;
		}

		private void SendSnapshotToAllClients(AnimSyncPacket packet, AnimStateSyncer syncer, float now)
		{
			using var _ = Profiler.Scope();

			PacketSender.SendToAllClients(packet, PacketSendMode.Unreliable);
			_syncStates[syncer].LastSentTime = now;
		}

		private bool ShouldBackOffForSteamQueue()
		{
			using var _ = Profiler.Scope();

			if (!NetworkConfig.IsSteamConfig())
				return false;

			foreach (var player in MultiplayerSession.ConnectedPlayers.Values)
			{
				if (player.PlayerId == MultiplayerSession.HostUserID)
					continue;
				if (player.Connection is not HSteamNetConnection connection)
					continue;

				SteamNetConnectionRealTimeStatus_t status = default;
				SteamNetConnectionRealTimeLaneStatus_t laneStatus = default;
				var result = SteamNetworkingSockets.GetConnectionRealTimeStatus(connection, ref status, 0, ref laneStatus);
				if (result != EResult.k_EResultOK)
					continue;

				// Queue pressure doubles correction intervals before unreliable traffic starts piling up.
				if (status.m_cbPendingUnreliable > PendingUnreliableBackoffBytes || (long)status.m_usecQueueTime > QueueTimeBackoffUsec)
					return true;
			}

			return false;
		}

	}
}
