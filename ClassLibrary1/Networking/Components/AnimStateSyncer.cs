using ONI_MP.Networking.Packets.Animation;
using Shared.Profiling;
using UnityEngine;

namespace ONI_MP.Networking.Components
{
	public class AnimStateSyncer : KMonoBehaviour, IRender1000ms
	{
		private const float ElapsedBucketSize = 0.15f;

		[MyCmpGet]
		private NetworkIdentity networkIdentity;
		[MyCmpGet]
		private KBatchedAnimController animController;
		[MyCmpGet]
		private KPrefabID prefabId;

		private int _lastSentAnimHash;
		private byte _lastSentMode;
		private float _lastSentSpeed = 1f;
		private int _lastSentElapsedBucket = int.MinValue;
		private bool _hasSentSnapshot;

		public override void OnSpawn()
		{
			using var _ = Profiler.Scope();

			base.OnSpawn();

			if (networkIdentity == null || animController == null || prefabId == null)
			{
				enabled = false;
				return;
			}

			if (!AnimSyncEligibility.IsAnimatedNonMinion(gameObject))
			{
				enabled = false;
				return;
			}
		}

		public void Render1000ms(float dt)
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.InSession || MultiplayerSession.IsClient)
				return;

			if (MultiplayerSession.ConnectedPlayers.Count == 0)
				return;

			SendSnapshot();
		}

		private void SendSnapshot()
		{
			using var _ = Profiler.Scope();

			try
			{
				if (networkIdentity.NetId == 0)
				{
					// Late-spawned entities may not have a NetId on the first render tick.
					networkIdentity.RegisterIdentity();
					if (networkIdentity.NetId == 0)
						return;
				}

				if (animController.CurrentAnim == null)
					return;

				int animHash = animController.currentAnim.hash;
				if (animHash == 0)
					return;

				byte mode = (byte)animController.mode;
				float speed = animController.playSpeed;
				int elapsedBucket = Mathf.RoundToInt(animController.GetElapsedTime() / ElapsedBucketSize);

				if (_hasSentSnapshot
					&& animHash == _lastSentAnimHash
					&& _lastSentMode == mode
					&& Mathf.Approximately(_lastSentSpeed, speed)
					&& _lastSentElapsedBucket == elapsedBucket)
				{
					// Only resend when the anim state changes buckets to keep per-entity traffic bounded.
					return;
				}

				var packet = new AnimSyncPacket
				{
					NetId = networkIdentity.NetId,
					AnimHash = animHash,
					Mode = mode,
					Speed = speed,
					ElapsedTime = elapsedBucket * ElapsedBucketSize
				};

				PacketSender.SendToAllClients(packet, PacketSendMode.Unreliable);

				_lastSentAnimHash = packet.AnimHash;
				_lastSentMode = packet.Mode;
				_lastSentSpeed = packet.Speed;
				_lastSentElapsedBucket = Mathf.RoundToInt(packet.ElapsedTime / ElapsedBucketSize);
				_hasSentSnapshot = true;
			}
			catch (System.Exception)
			{
				// Anim state may not be ready yet.
			}
		}
	}
}
