#nullable enable
using HarmonyLib;

namespace Disguise;

/// <summary>
/// RPC_Step does "m_effects[effectIndex]" with no bounds check: the index comes from the list
/// of the object held by the machine that SENT it, and vanilla relies on every machine
/// instantiating the same prefab for that ZDO. A disguised player is instantiated as a Player
/// here (Patch_CreateObject) and as a creature everywhere vanilla, so the two lists disagree in
/// both directions - Patch_FootStepFindBestStepEffect fixes what our side emits, and this is
/// the safety net for what comes back: creature indices arriving at our Player copy (the echo
/// of our own remapped step, or a step emitted by a vanilla observer running the creature's
/// animator).
///
/// An out-of-range index is skipped instead of thrown: on the receiving machine the effect is
/// simply not played, which is what vanilla would do if it ever noticed the mismatch. It cannot
/// hide a real fault - while not disguised the index is the vanilla one and always in range.
/// </summary>
[HarmonyPatch(typeof(FootStep), nameof(FootStep.RPC_Step))]
internal static class Patch_FootStepRPCStep
{
    private static bool Prefix(FootStep __instance, int effectIndex)
    {
        return effectIndex >= 0 && effectIndex < __instance.m_effects.Count;
    }
}
