using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using ONI_MP.DebugTools;
using ONI_MP.Networking.Components;
using Shared.Profiling;
using UnityEngine;

namespace ONI_MP.Patches.Critters
{
	internal class EntityTemplatesPatch
	{
		[HarmonyPatch(typeof(EntityTemplates), nameof(EntityTemplates.ExtendEntityToBasicCreature), new Type[] { typeof(bool), typeof(GameObject), typeof(string), typeof(string), typeof(string), typeof(FactionManager.FactionID), typeof(string), typeof(string), typeof(NavType), typeof(int), typeof(float), typeof(string), typeof(float), typeof(bool), typeof(bool), typeof(float), typeof(float), typeof(float), typeof(float) })]
		public static class ExtendEntityToBasicCreature_Patch
		{
			public static void Postfix(GameObject __result)
			{
				using var _ = Profiler.Scope();

				if (__result == null)
					return;

				if (!__result.HasTag(GameTags.Creature))
					return;

				__result.AddOrGet<EntityPositionHandler>();

				var kbac = __result.GetComponent<KBatchedAnimController>();
				if (kbac == null)
					return;

				var identity = __result.AddOrGet<NetworkIdentity>();
				identity.RegisterIdentity();
				__result.AddOrGet<AnimStateSyncer>();
			}
		}
	}
}
