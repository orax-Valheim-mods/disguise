#nullable enable
using System;
using HarmonyLib;

namespace Disguise;

/// <summary>
/// ZDO.Serialize is the NETWORK path (its only caller is ZDOMan.SendZDOs, the save file
/// goes through ZDO.Save): for the duration of the packet, EquipmentMask hides the enabled
/// equipment keys of a disguised player, HealthMask writes the costume's own maximum into
/// its health key, RestOffset lays the costume on the ground its player stands on, and
/// PoseControl turns it the way its owner asked for.
///
/// Vanilla clients therefore receive 0 for HelmetItem / ChestItem / LegItem / ShoulderItem /
/// LeftItem... and never glue your gear to the creature rig, and they render a costume that
/// is whole instead of one wearing your hit points as damage (HealthMask has the reason);
/// modded clients receive the real equipment hashes in our own cd_eq_* keys instead. The
/// rotation is the one the wire cannot carry any other way: ZDO.Serialize is where the body
/// writes it (the packet header carries a position, never a rotation), so a costume lying on
/// a slope is only a slope on the machines that render it while this prefix holds it down -
/// Relaying machine only, like the health (see RestOffset for both) - and a costume the owner
/// turned stays turned while PoseControl composes cd_rx / cd_ry / cd_rz into that same value
/// (its swap opens AFTER RestOffset's and closes before it, so the turn ends up on top of the
/// tilt and one single rotation leaves the server - the other order would erase it, since a
/// lay-on-the-ground is a FromToRotation onto the ground normal: it forces the up axis of
/// whatever it is applied to, which is exactly the pitch and the roll of the turn). None of
/// the four is reflected in the DataRevision, so they cost no extra traffic and never change
/// the local ZDO state (your own view).
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
            // Ground first, turn on top of it - the order SelfView gives the clone, and the
            // only one that leaves the pitch and the roll of the turn standing.
            RestOffset.BeginOutgoingRotation(__instance);
            PoseControl.BeginOutgoingRotation(__instance);
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Patch ZDO.Serialize (prefix): {ex}");
        }
        return true;
    }

    // The finalizer runs even if the original method threw: a swap must never outlive the
    // call it was opened for. HealthMask.End, RestOffset.EndRotation and
    // PoseControl.EndOutgoingRotation close nothing when their Begin opened nothing. The two
    // rotation swaps close in the reverse order they were opened in: each puts back the value
    // IT found, so the one that opened last has to hand its predecessor's value back first.
    private static Exception? Finalizer(Exception? __exception, bool __state)
    {
        if (__state)
        {
            EquipmentMask.End();
        }
        HealthMask.End();
        PoseControl.EndOutgoingRotation();
        RestOffset.EndRotation();
        return __exception;
    }
}
