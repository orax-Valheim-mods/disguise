#nullable enable
using System;
using HarmonyLib;

namespace Disguise;

/// <summary>
/// ZDO.Serialize is the NETWORK path (its only caller is ZDOMan.SendZDOs, the save file
/// goes through ZDO.Save): for the duration of the packet, EquipmentMask hides the enabled
/// equipment keys of a disguised player.
///
/// Vanilla clients therefore receive 0 for HelmetItem / ChestItem / LegItem / ShoulderItem /
/// LeftItem... and never glue your gear to the creature rig; modded clients receive the real
/// hashes in our own cd_eq_* keys instead. The swap is not reflected in the DataRevision, so
/// it costs no extra traffic and never changes the local ZDO state (your own view).
///
/// This runs on every machine owning a disguised ZDO, but also on the server relaying it:
/// the masking then works even when the owner's client has no DLL (e.g. a disguise kept in
/// the save).
/// </summary>
[HarmonyPatch(typeof(ZDO), nameof(ZDO.Serialize))]
internal static class Patch_ZDOSerialize
{
    private static bool Prefix(ZDO __instance, out bool __state)
    {
        __state = false;
        try
        {
            __state = EquipmentMask.BeginOutgoingMask(__instance);
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Patch ZDO.Serialize (prefix): {ex}");
        }
        return true;
    }

    // The finalizer runs even if the original method threw: a swap must never outlive the
    // call it was opened for.
    private static Exception? Finalizer(Exception? __exception, bool __state)
    {
        if (__state)
        {
            EquipmentMask.End();
        }
        return __exception;
    }
}
