using System.Collections.Generic;
using ONI_MP.Misc;
using ONI_MP.Networking.Packets.Animation;
using Shared.Profiling;
using UnityEngine;

namespace ONI_MP.Networking.Components
{
	internal class AnimResyncRequester : MonoBehaviour
	{
		private const float InitialRequestDelay = 1f;
		private const float RetryInterval = 5f;

		private bool _subscribed;
		private bool _initialRequestSent;
		private float _nextInitialRequestTime = float.MaxValue;
		private float _lastRetryTime;

		private void Update()
		{
			using var _ = Profiler.Scope();

			if (Game.Instance != null && !_subscribed)
				SubscribeToGameHashes();

			if (!MultiplayerSession.IsClient || !MultiplayerSession.InSession || !Utils.IsInGame())
			{
				_initialRequestSent = false;
				_nextInitialRequestTime = float.MaxValue;
				return;
			}

			if (!_initialRequestSent && _nextInitialRequestTime == float.MaxValue)
				_nextInitialRequestTime = Time.unscaledTime + InitialRequestDelay;

			float now = Time.unscaledTime;
			if (!_initialRequestSent && now >= _nextInitialRequestTime && RequestVisibleAnimations(true))
			{
				_initialRequestSent = true;
				_nextInitialRequestTime = float.MaxValue;
				_lastRetryTime = now;
			}

			if (_initialRequestSent && now - _lastRetryTime >= RetryInterval)
			{
				RequestVisibleAnimations(false);
				_lastRetryTime = now;
			}
		}

		private void SubscribeToGameHashes()
		{
			using var _ = Profiler.Scope();

			Game.Instance.Subscribe(MP_HASHES.GameClient_OnConnectedInGame, ScheduleInitialRequest);
			Game.Instance.Subscribe(MP_HASHES.OnMultiplayerGameSessionInitialized, ScheduleInitialRequest);
			_subscribed = true;
		}

		private void ScheduleInitialRequest(object _ = null)
		{
			using var scope = Profiler.Scope();

			_initialRequestSent = false;
			_nextInitialRequestTime = Time.unscaledTime + InitialRequestDelay;
			_lastRetryTime = 0f;
		}

		private bool RequestVisibleAnimations(bool includeAllVisible)
		{
			using var _ = Profiler.Scope();

			if (!WorldStateSyncer.TryGetLocalViewport(out var viewport, 2))
				return false;

			var requestedNetIds = new HashSet<int>();
			foreach (var syncer in AnimSyncCoordinator.GetTrackedSyncers())
			{
				if (syncer == null || !syncer.IsVisibleIn(viewport))
					continue;
				// Only retry visible entities that never received an authoritative snapshot.
				if (!includeAllVisible && !syncer.NeedsInitialSnapshot())
					continue;
				if (syncer.NetId == 0)
					continue;

				requestedNetIds.Add(syncer.NetId);
			}

			if (requestedNetIds.Count > 0)
			{
				PacketSender.SendToHost(new AnimResyncRequestPacket
				{
					RequesterId = MultiplayerSession.LocalUserID,
					NetIds = [.. requestedNetIds]
				}, PacketSendMode.Unreliable);
			}

			return true;
		}
	}
}
