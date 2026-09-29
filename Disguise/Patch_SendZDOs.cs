#nullable enable
using System;
using HarmonyLib;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Server: while an observer refresh is armed, ZDOMan.SendZDOs writes a fake far-away
/// position for the disguised player ZDO into the outgoing packet (the receiving vanilla
/// client then re-creates its instance with the current prefab).
///
/// The real position is stashed in the Prefix and written back in the Postfix / Finalizer:
/// only the packet is faked, the server's ZDO state - and therefore saves and game logic -
/// always keeps the real position.
///
/// NOTE: no per-peer filtering here (ZDOMan.ZDOPeer is a private type of the game): your own
/// connection cannot apply the fake anyway, because a ZDO update is only applied when its
/// DataRevision is STRICTLY newer than yours, and you are the author of every revision of
/// your own player ZDO (RPC_ZDOData, "num4 <= zDO.DataRevision -> continue").
/// Patch_FindSectorObjects is the second safety net, on top of that.
/// </summary>
[HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.SendZDOs))]
internal static class Patch_SendZDOs
{
    private static void Prefix()
    {
        try
        {
            if (Disguise.s_savedPosition.HasValue)
            {
                // Already inside a swap (defensive: no nested SendZDOs expected).
                return;
            }
            ZDO? zdo = Disguise.s_refreshZdo;
            if (zdo == null || Disguise.s_zdoPositionField == null)
            {
                return;
            }
            Disguise.s_savedPosition = (Vector3?)Disguise.s_zdoPositionField.GetValue(zdo);
            Disguise.s_savedZdo = zdo;
            Disguise.s_zdoPositionField.SetValue(zdo, Disguise.s_refreshFarPos);
        }
        catch (Exception ex)
        {
            Disguise.s_savedPosition = null;
            Disguise.s_savedZdo = null;
            Disguise.LogError($"Patch SendZDOs (prefix): {ex}");
        }
    }

    private static void Postfix()
    {
        RestoreRealPosition();
    }

    // The finalizer runs even if the original method threw: a fake position must never
    // survive the packet-building call.
    private static Exception? Finalizer(Exception? __exception)
    {
        RestoreRealPosition();
        return __exception;
    }

    private static void RestoreRealPosition()
    {
        if (Disguise.s_savedPosition is not Vector3 saved)
        {
            return;
        }
        Disguise.s_savedPosition = null;
        ZDO? zdo = Disguise.s_savedZdo;
        Disguise.s_savedZdo = null;
        if (zdo == null || Disguise.s_zdoPositionField == null)
        {
            return;
        }
        try
        {
            Disguise.s_zdoPositionField.SetValue(zdo, saved);
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Patch SendZDOs (restore): {ex}");
        }
    }
}
