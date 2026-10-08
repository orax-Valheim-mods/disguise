#nullable enable
using HarmonyLib;
using UnityEngine;

namespace Disguise;

/// <summary>
/// ZDOMan.SendZDOs writes the position of every outgoing ZDO through ZDO.GetPosition: that
/// per-ZDO header is the ONLY position the wire carries - the receiver applies it with
/// ZDO.InternalSetPosition before deserializing the rest, and ZDO.Deserialize never reads a
/// position (the other writer of one, ZDO.Save, is the save file).
///
/// While that call builds a packet on the relaying machine, this therefore hands back the
/// position of a disguised player moved by the offset published in cd_ox / cd_oy / cd_oz and
/// raised by the height published in cd_fly, so every machine that only renders you - a
/// vanilla client above all - draws the creature where the owner asked for it, held inside a
/// small band around the one already sent (PositionHold), so that the few millimetres of the
/// owner's physics jitter cannot travel and make the costume shiver, and lifted to where the
/// costume rests rather than digs into the ground (RestOffset), which is what keeps the
/// receiving body from fighting back. All four hand back a copy: nothing is written into the
/// ZDO, so the server's own state, the saves and the game logic keep the grounded player.
///
/// The window (server only, creature prefabs only) is opened and closed by Patch_SendZDOs
/// around that one call; the tests live in PositionHold.Hold, RestOffset.Raise,
/// PoseControl.RaiseSentPosition and FlightControl.RaiseSentPosition.
///
/// NOTE: SendZDOs also sorts what it is about to send by distance (ServerSortSendZDOS), so
/// while the window is open a flying player sorts up to 100 m farther than it really is -
/// that only reorders a packet that is full, never a position.
/// </summary>
[HarmonyPatch(typeof(ZDO), nameof(ZDO.GetPosition))]
internal static class Patch_ZDOGetPosition
{
    private static void Postfix(ZDO __instance, ref Vector3 __result)
    {
        // Four passes over the same copy: hold the real position inside the band first, then
        // lift it to where the costume rests, then add the offset of cd_ox / cd_oy / cd_oz
        // and finally the height of cd_fly on top - the band stays the one the owner covers,
        // and the three lifts never see each other's value.
        PositionHold.Hold(__instance, ref __result);
        RestOffset.Raise(__instance, ref __result);
        PoseControl.RaiseSentPosition(__instance, ref __result);
        FlightControl.RaiseSentPosition(__instance, ref __result);
    }
}
