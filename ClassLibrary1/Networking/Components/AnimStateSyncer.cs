using ONI_MP.Networking.Packets.Animation;
using Shared.Profiling;
using UnityEngine;

namespace ONI_MP.Networking.Components
{
	public class AnimStateSyncer : KMonoBehaviour
	{
		private const float SendInterval = 1f;
		private const float InitialDelay = 5f;
		private const float ElapsedBucketSize = 0.15f;

		[MyCmpGet]
		private NetworkIdentity networkIdentity;
		[MyCmpGet]
		private KBatchedAnimController animController;
		[MyCmpGet]
		private KPrefabID prefabId;

		private bool _initialized;
		private float _initializationTime;
		private float _lastSendTime;
		private int _lastSentAnimHash;
		private byte _lastSentMode;
		private float _lastSentSpeed = 1f;
		private int _lastSentElapsedBucket = int.MinValue;

		public override void OnSpawn()
		{
			using var _ = Profiler.Scope();

			base.OnSpawn();

			if (networkIdentity == null || animController == null || prefabId == null)
			{
				enabled = false;
				return;
			}

			if (prefabId.HasTag(GameTags.BaseMinion))
			{
				enabled = false;
				return;
			}

			if (!prefabId.HasTag(GameTags.Creature) && GetComponent<BuildingComplete>() == null)
			{
				enabled = false;
				return;
			}
		}

		private void Update()
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.InSession || MultiplayerSession.IsClient)
				return;

			if (MultiplayerSession.ConnectedPlayers.Count == 0)
				return;

			if (!_initialized)
			{
				_initializationTime = Time.unscaledTime;
				_initialized = true;
				return;
			}

			if (Time.unscaledTime - _initializationTime < InitialDelay)
				return;

			float currentTime = Time.unscaledTime;
			if (currentTime - _lastSendTime < SendInterval)
				return;

			SendSnapshot(currentTime);
		}

		private void SendSnapshot(float currentTime)
		{
			using var _ = Profiler.Scope();

			try
			{
				if (animController.CurrentAnim == null)
					return;

				int animHash = animController.currentAnim.hash;
				if (animHash == 0)
					return;

				_lastSentAnimHash = animHash;
				_lastSentMode = (byte)animController.mode;
				_lastSentSpeed = animController.playSpeed;
				_lastSentElapsedBucket = Mathf.RoundToInt(animController.GetElapsedTime() / ElapsedBucketSize);
				_lastSendTime = currentTime;

				var packet = new AnimSyncPacket
				{
					NetId = networkIdentity.NetId,
					AnimHash = _lastSentAnimHash,
					Mode = _lastSentMode,
					Speed = _lastSentSpeed,
					ElapsedTime = _lastSentElapsedBucket * ElapsedBucketSize
				};

				PacketSender.SendToAllClients(packet, PacketSendMode.Unreliable);
			}
			catch (System.Exception)
			{
				// Anim state may not be ready yet.
			}
		}
	}
}
