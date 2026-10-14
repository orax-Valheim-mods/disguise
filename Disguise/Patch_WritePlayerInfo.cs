#nullable enable
using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Disguise;

/// <summary>
/// Server: writes what the OWNER published into the outgoing player list instead of what
/// the server holds - the fake map position (cd_fake / cd_fpos, FakePosition) and the
/// hiding itself (cd_offlist, OffList). That list is the map and the session itself:
/// SendPlayerList -> RPC_PlayerList on every client -> ConnectPanel (the F2 "Players" line
/// and count), SessionPlayerList, Minimap through GetOtherPublicPlayers.
///
/// Only the packet is ever modified (ApplyTo / Restore, both concerns): the ZDO position,
/// peer.m_refPos - which the server also uses to decide which ZDOs a peer receives
/// (ZDOMan.CreateSyncList) - the server's own list, the save file and the console all
/// keep the real state, and so does your own screen.
///
/// ORDER, which is the whole subtlety of the two together: OffList takes entries OUT of
/// the list, and a removal shifts every index after it - so it runs FIRST, and
/// FakePosition then walks the shortened list (only entries still in it can carry a fake
/// position at all, and an entry hidden from the others needs no position behind it).
/// The restores unwind in the opposite order: the positions back into the shortened list,
/// then the entries back into the whole one.
///
/// NOTE: WritePlayerInfo has a single caller (ZNet.SendPlayerList), reached from four
/// places that all sit behind IsServer / m_isServer; the extra check in the Prefix is the
/// guarantee that no client can ever see a modified list, not even its own. That caller
/// passes m_players itself, and the method serializes m_players.Count off the FIELD rather
/// than the count of the list it walks: the two are the same list, so an entry OffList
/// removes leaves the count right exactly like the packet.
/// </summary>
[HarmonyPatch(typeof(ZNet), nameof(ZNet.WritePlayerInfo))]
internal static class Patch_WritePlayerInfo
{
    private static void Prefix(List<ZNet.PlayerInfo> __0)
    {
        try
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer())
            {
                return;
            }
            OffList.ApplyTo(__0);
            FakePosition.ApplyTo(__0);
        }
        catch (Exception ex)
        {
            FakePosition.Restore();
            OffList.Restore();
            Disguise.LogError($"Patch WritePlayerInfo (prefix): {ex}");
        }
    }

    private static void Postfix()
    {
        FakePosition.Restore();
        OffList.Restore();
    }

    // The finalizer runs even if the original method threw: neither a fake position nor a
    // removed entry must ever survive the packet-building call.
    private static Exception? Finalizer(Exception? __exception)
    {
        FakePosition.Restore();
        OffList.Restore();
        return __exception;
    }
}
