#nullable enable
using System;
using HarmonyLib;

namespace Disguise;

/// <summary>
/// ZDO.Serialize, second window: while the packet is written, the relation the owner
/// published (Disguise.RelationKey) is written into ZDOVars.s_tamed, so a vanilla client
/// renders your creature with the green bar of an ally or the red one of an enemy
/// (ShowAsAlly). The value is restored right after the call: the stored ZDO, the save file
/// and your own view are untouched, and the swap never bumps the DataRevision.
///
/// A separate class from Patch_ZDOSerialize on purpose: each window then keeps its own
/// __state, so an exception in one can never leave the other one open - the two windows are
/// independent and may be nested in any order.
/// </summary>
[HarmonyPatch(typeof(ZDO), nameof(ZDO.Serialize))]
internal static class Patch_ZDOSerializeRelation
{
    private static bool Prefix(ZDO __instance, out bool __state)
    {
        __state = false;
        try
        {
            __state = AllyView.BeginSerialize(__instance);
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Patch ZDO.Serialize relation (prefix): {ex}");
        }
        return true;
    }

    // The finalizer runs even if the original method threw: a swap must never outlive the
    // call it was opened for.
    private static Exception? Finalizer(Exception? __exception, bool __state)
    {
        if (__state)
        {
            AllyView.End();
        }
        return __exception;
    }
}
