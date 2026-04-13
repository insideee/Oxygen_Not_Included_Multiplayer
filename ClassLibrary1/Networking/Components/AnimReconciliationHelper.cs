using HarmonyLib;
using ONI_MP.DebugTools;
using ONI_MP.Patches.KleiPatches;
using System;
using System.Reflection;
using UnityEngine;

namespace ONI_MP.Networking.Components
{
	/// <summary>
	/// Static helper for setting animation elapsed time via reflection.
	/// Used by DuplicantStatePacket for continuous animation reconciliation.
	/// Resolves SetElapsedTime method or elapsedTime field once, then caches.
	/// </summary>
	internal static class AnimReconciliationHelper
	{
		private const float DriftThreshold = 0.15f;
		private static MethodInfo _setElapsedTimeMethod;
		private static FieldInfo _elapsedTimeField;
		private static bool _resolved;

		internal static void Reconcile(KBatchedAnimController kbac, HashedString animHash, KAnim.PlayMode playMode, float animSpeed, float elapsedTime, string source)
		{
			try
			{
				if (kbac.currentAnim != animHash)
				{
					KAnimControllerBase_Patches.AllowAnims();
					kbac.Play(animHash, playMode, animSpeed, 0f);
					KAnimControllerBase_Patches.ForbidAnims();
					ForceAnimUpdate(kbac, source);
					TrySetElapsedTime(kbac, elapsedTime);
					return;
				}

				float localElapsed = kbac.GetElapsedTime();
				if (Mathf.Abs(localElapsed - elapsedTime) > DriftThreshold)
					TrySetElapsedTime(kbac, elapsedTime);
			}
			catch (Exception ex)
			{
				DebugConsole.LogWarning($"[{source}] Anim reconciliation failed: {ex}");
			}
		}

		internal static void TrySetElapsedTime(KAnimControllerBase kbac, float elapsedTime)
		{
			if (!_resolved)
			{
				_resolved = true;
				_setElapsedTimeMethod = AccessTools.Method(typeof(KAnimControllerBase), "SetElapsedTime", [typeof(float)]);
				if (_setElapsedTimeMethod == null)
					_elapsedTimeField = AccessTools.Field(typeof(KAnimControllerBase), "elapsedTime");
			}

			try
			{
				if (_setElapsedTimeMethod != null)
					_setElapsedTimeMethod.Invoke(kbac, [elapsedTime]);
				else if (_elapsedTimeField != null)
					_elapsedTimeField.SetValue(kbac, elapsedTime);
			}
			catch (Exception ex)
			{
				DebugConsole.LogWarning($"[AnimReconciliationHelper] Failed to set elapsed time: {ex}");
			}
		}

		internal static void ForceAnimUpdate(KBatchedAnimController kbac, string source)
		{
			try
			{
				kbac.SetVisiblity(true);
				kbac.forceRebuild = true;
				kbac.SuspendUpdates(false);
				kbac.ConfigureUpdateListener();
			}
			catch (Exception ex)
			{
				DebugConsole.LogError($"[{source}] ForceAnimUpdate failed: {ex}");
			}
		}
	}
}
