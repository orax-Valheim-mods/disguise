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
/// position of a disguised player raised by the height published in cd_fly, so every machine
/// that only renders you - a vanilla client above all - draws the creature in the air.
/// GetPosition returns a copy: nothing is written into the ZDO, so the server's own state,
/// the saves and the game logic keep the grounded player.
///
/// The window (server only, creature prefab only) is opened and closed by Patch_SendZDOs
/// around that one call; the whole test lives in FlightControl.RaiseSentPosition.
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
        FlightControl.RaiseSentPosition(__instance, ref __result);
    }
}
