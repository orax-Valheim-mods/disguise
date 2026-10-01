#nullable enable
using System;
using HarmonyLib;

namespace Disguise;

/// <summary>
/// FootStep.FindBestStepEffect returns an index into THIS instance's own m_effects list, and
/// both callers (OnFoot, OnLand) broadcast it to every machine, where RPC_Step indexes the list
/// of the object instantiated here. Vanilla never notices: every machine builds the same prefab
/// for that ZDO. A disguised player breaks exactly that: machines running this DLL build a real
/// Player (Patch_CreateObject) while vanilla machines build the creature - two different lists,
/// and the vanilla side throws ArgumentOutOfRangeException on every step and every landing.
///
/// So for a marked (disguised) player ZDO the index is resolved against the creature prefab's
/// own list, which is the list every vanilla machine holds: the creature plays its own effect
/// for the ground and motion, instead of a player index landing in a foreign list. -1 when the
/// creature has no FootStep or no matching effect, read by both callers as "nothing to send".
/// Machines running this DLL receive that creature index against their Player list, which is
/// what Patch_FootStepRPCStep tolerates: no combination of vanilla / modded observers throws.
///
/// The check does not require ownership: on any machine running this DLL a marked ZDO holds a
/// Player object, including an observer's copy of someone else's disguise, and every one of
/// them would emit player indices. FindBestStepEffect itself is private in the game (hence
/// GameAccess), and the original arguments are passed through untouched: they are typed with
/// FootStep's own enums, which this project never references directly.
/// </summary>
[HarmonyPatch(typeof(FootStep), nameof(FootStep.FindBestStepEffect))]
internal static class Patch_FootStepFindBestStepEffect
{
    private static bool Prefix(FootStep __instance, object[] __args, ref int __result)
    {
        try
        {
            if (IsDisguised(__instance))
            {
                __result = GameAccess.FindBestStepEffect(PrefabResolver.CreatureFootStep(), __args);
                return false;
            }
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Patch FootStep.FindBestStepEffect (prefix): {ex}");
        }
        return true;
    }

    /// <summary>True when this instance carries a player ZDO marked as disguised.</summary>
    private static bool IsDisguised(FootStep step)
    {
        // FootStep.m_nview is private in the game: the same lookup FootStep.Start does.
        ZNetView? nview = step.GetComponent<ZNetView>();
        return nview != null && nview.IsValid() && DisguiseApplier.HasMarker(nview.GetZDO());
    }
}
