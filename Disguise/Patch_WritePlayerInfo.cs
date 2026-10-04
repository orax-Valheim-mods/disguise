#nullable enable
using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Disguise;

/// <summary>
/// Server: writes the fake map position published by a player (cd_fake / cd_fpos) into the
/// outgoing player list instead of the real one. That list is the map itself:
/// SendPlayerList -> RPC_PlayerList on every client -> Minimap.UpdatePlayerPins, and
/// GetOtherPublicPlayers has no other reader.
///
/// Only the packet is faked (FakePosition.ApplyTo / Restore): the ZDO position,
/// peer.m_refPos - which the server also uses to decide which ZDOs a peer receives
/// (ZDOMan.CreateSyncList) - and the save file always keep the real position, and so does
/// your own screen.
///
/// NOTE: WritePlayerInfo has a single caller (ZNet.SendPlayerList), reached from four
/// places that all sit behind IsServer / m_isServer; the extra check in the Prefix is the
/// guarantee that no client can ever see a modified list, not even its own.
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
            FakePosition.ApplyTo(__0);
        }
        catch (Exception ex)
        {
            FakePosition.Restore();
            Disguise.LogError($"Patch WritePlayerInfo (prefix): {ex}");
        }
    }

    private static void Postfix()
    {
        FakePosition.Restore();
    }

    // The finalizer runs even if the original method threw: a fake position must never
    // survive the packet-building call.
    private static Exception? Finalizer(Exception? __exception)
    {
        FakePosition.Restore();
        return __exception;
    }
}
