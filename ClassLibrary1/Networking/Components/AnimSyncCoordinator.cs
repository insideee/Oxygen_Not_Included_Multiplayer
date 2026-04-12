using System.Collections.Generic;
using ONI_MP.Networking.Packets.Animation;
using Shared.Profiling;
using UnityEngine;

namespace ONI_MP.Networking.Components
{
	internal class AnimSyncCoordinator : MonoBehaviour
	{
		private class SyncState
		{
			public bool HasObservedState;
			public int LastActivityKey;
			public float LastSentTime;
		}

		private const float TickInterval = 0.2f;
		private const int ShardCount = 5;
		private const float SyncInterval = 1f;

		private static readonly HashSet<AnimStateSyncer> TrackedSyncers = [];

		public static AnimSyncCoordinator Instance { get; private set; }

		private readonly Dictionary<AnimStateSyncer, SyncState> _syncStates = [];
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

			var trackedSyncers = GetTrackedSyncers();
			if (trackedSyncers.Count == 0)
				return;

			// Spread the full scan across five 200ms ticks to avoid bursty correction traffic.
			for (int i = 0; i < trackedSyncers.Count; i++)
			{
				if (i % ShardCount != _currentShard)
					continue;

				ProcessSyncer(trackedSyncers[i]);
			}

			_currentShard = (_currentShard + 1) % ShardCount;
		}

		private void ProcessSyncer(AnimStateSyncer syncer)
		{
			using var _ = Profiler.Scope();

			if (syncer == null)
				return;

			if (!syncer.TryBuildSnapshot(out var packet, out var activityKey))
				return;

			float now = Time.unscaledTime;
			bool activityChanged = UpdateObservedState(syncer, activityKey);
			if (!activityChanged && now - _syncStates[syncer].LastSentTime < SyncInterval)
				return;

			PacketSender.SendToAllClients(packet, PacketSendMode.Unreliable);
			_syncStates[syncer].LastSentTime = now;
		}

		private bool UpdateObservedState(AnimStateSyncer syncer, int activityKey)
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
			}

			return activityChanged;
		}

		private static List<AnimStateSyncer> GetTrackedSyncers()
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
	}
}
