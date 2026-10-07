#nullable enable
using System;
using HarmonyLib;

namespace Disguise;

/// <summary>
/// ZDO.Serialize is the NETWORK path (its only caller is ZDOMan.SendZDOs, the save file
/// goes through ZDO.Save): for the duration of the packet, EquipmentMask hides the enabled
/// equipment keys of a disguised player, HealthMask writes the costume's own maximum into
/// its health key, and RestOffset lays the costume on the ground its player stands on.
///
/// Vanilla clients therefore receive 0 for HelmetItem / ChestItem / LegItem / ShoulderItem /
/// LeftItem... and never glue your gear to the creature rig, and they render a costume that
/// is whole instead of one wearing your hit points as damage (HealthMask has the reason);
/// modded clients receive the real equipment hashes in our own cd_eq_* keys instead. The
/// rotation is the one the wire cannot carry any other way: ZDO.Serialize is where the body
/// writes it (the packet header carries a position, never a rotation), so a costume lying on
/// a slope is only a slope on the machines that render it while this prefix holds it down -
/// Relaying machine only, like the health (see RestOffset for both). None of the three is
/// reflected in the DataRevision, so they cost no extra traffic and never change the local
/// ZDO state (your own view).
///
/// This runs on every machine owning a disguised ZDO, but also on the server relaying it:
/// what gets masked there follows the owner's own choice, published in its ZDO as
/// Disguise.HideKey - the relaying machine's [3 - Display] config is only the fallback for
/// an owner whose client has no DLL (e.g. a disguise kept in the save), never an override.
/// The health and rotation swaps are the relaying machine's alone.
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
            HealthMask.BeginOutgoingHealth(__instance);
            RestOffset.BeginOutgoingRotation(__instance);
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Patch ZDO.Serialize (prefix): {ex}");
        }
        return true;
    }

    // The finalizer runs even if the original method threw: a swap must never outlive the
    // call it was opened for. HealthMask.End and RestOffset.EndRotation close nothing when
    // their Begin opened nothing.
    private static Exception? Finalizer(Exception? __exception, bool __state)
    {
        if (__state)
        {
            EquipmentMask.End();
        }
        HealthMask.End();
        RestOffset.EndRotation();
        return __exception;
    }
}
