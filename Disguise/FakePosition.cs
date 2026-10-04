#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Fake map position: what the OTHER players see as your position on their map.
///
/// Valheim carries a player's position over two unrelated channels:
///  - the ZDO position - where your character is rendered, and what ZDOMan uses to decide
///    which ZDOs a peer receives (Patch_SendZDOs / observer refresh);
///  - ZNet's player list (ZNet.SendPlayerList -> RPC_PlayerList), fed by the reference
///    position each machine reports (ZNet.m_referencePosition / ZNetPeer.m_refPos).
///    THIS is the map: Minimap.UpdatePlayerPins reads it through GetOtherPublicPlayers,
///    which is the only reader of that list.
///
/// So the fake concerns the player list only, and it is the OWNER that decides: the
/// position to display travels in its own ZDO (cd_fake + cd_fpos), and the modded server
/// writes it into the outgoing player list instead of the real one, for the duration of
/// the write only (Patch_WritePlayerInfo). Everything the server decides with - the
/// ZDO position, peer.m_refPos (which selects the ZDOs a peer receives,
/// ZDOMan.CreateSyncList), the save file - keeps the real position, and so does your own
/// screen: only the maps of the others move.
///
/// Same contract as cd_hide / cd_rel: a client publishing no DLL publishes nothing and
/// there is nothing to fake; a server running no DLL has no patch to write the fake
/// position, so its player list keeps the real one.
/// </summary>
internal static class FakePosition
{
    // -- OWNER: the position this machine asks the others to display --------------------

    /// <summary>Null while the real position is shown (session state, dropped on restart).</summary>
    private static Vector3? s_fake;

    // -- SERVER: the player-list entries currently swapped (always restored) -------------

    /// <summary>The list being written (the swap is written back into it, never left behind).</summary>
    private static List<ZNet.PlayerInfo>? s_players;

    /// <summary>Indexes of the entries swapped by the current call, and their real positions.</summary>
    private static readonly List<int> s_swapped = new();

    private static readonly List<Vector3> s_saved = new();

    /// <summary>SERVER: players already reported once in the log (one line each).</summary>
    private static readonly HashSet<ZDOID> s_reported = new();

    /// <summary>The fake position published by this machine, null while the real one is used.</summary>
    internal static Vector3? Current => s_fake;

    internal static void Set(Vector3 position)
    {
        s_fake = position;
    }

    internal static void Clear()
    {
        s_fake = null;
    }

    /// <summary>
    /// Writes the choice into the local player ZDO: the owner is the only machine allowed
    /// to decide, relaying machines only follow. The disabling value is an explicit 0 -
    /// never a removed key, because ZDO.Deserialize only adds/overwrites the keys it
    /// receives (ZDOExtraData.Add never deletes): a removed key would simply be absent
    /// from the payload and the server would keep faking the position forever. Set() bumps
    /// the DataRevision only when the value really changes, so publishing it on every
    /// reassert costs nothing.
    /// </summary>
    internal static void Publish(ZDO? zdo)
    {
        if (zdo == null || !zdo.IsValid())
        {
            return;
        }
        int enabled = s_fake.HasValue ? 1 : 0;
        // Default 0, not -1: an absent key already means "not faking", so a player who
        // never asked for it never gets one written into the ZDO.
        if (zdo.GetInt(Disguise.FakeKey, 0) != enabled)
        {
            zdo.Set(Disguise.FakeKey, enabled);
        }
        if (s_fake is Vector3 position)
        {
            // X and Z are what Minimap draws; Y is only carried along so that the stored
            // value stays a sane world position.
            zdo.Set(Disguise.FakePositionKey, position);
        }
    }

    /// <summary>
    /// SERVER: replaces the position of every player that published one by its fake,
    /// before WritePlayerInfo serializes the list. The list holds structs, so the entry is
    /// modified and written back; Restore puts every real position where it found it.
    /// </summary>
    internal static void ApplyTo(List<ZNet.PlayerInfo> players)
    {
        Restore(); // defensive: swaps are never nested, and never survive the write
        if (players == null || players.Count == 0 || ZDOMan.instance == null)
        {
            return;
        }
        s_players = players;
        for (int i = 0; i < players.Count; i++)
        {
            ZNet.PlayerInfo info = players[i];
            // Without a public position nothing of it enters the packet: nothing to fake.
            if (!info.m_publicPosition)
            {
                continue;
            }
            ZDO? zdo = ZDOMan.instance.GetZDO(info.m_characterID);
            if (zdo == null || !zdo.IsValid() || zdo.GetInt(Disguise.FakeKey, 0) != 1)
            {
                continue;
            }
            // Absent cd_fpos -> the real position, so a half-published state fakes nothing.
            Vector3 fake = zdo.GetVec3(Disguise.FakePositionKey, info.m_position);
            s_swapped.Add(i);
            s_saved.Add(info.m_position);
            info.m_position = fake;
            players[i] = info;
            Report(zdo, fake);
        }
    }

    /// <summary>
    /// Puts every real position back. Called after the write and, through the finalizer,
    /// when the original method threw; it is idempotent, so the player list can never
    /// leave this call with a position that is not the real one.
    /// </summary>
    internal static void Restore()
    {
        List<ZNet.PlayerInfo>? players = s_players;
        int count = s_swapped.Count;
        s_players = null;
        if (players != null)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                int index = s_swapped[i];
                if (index < 0 || index >= players.Count)
                {
                    continue;
                }
                ZNet.PlayerInfo info = players[index];
                info.m_position = s_saved[i];
                players[index] = info;
            }
        }
        s_swapped.Clear();
        s_saved.Clear();
    }

    /// <summary>
    /// Server log, once per faking player: the only sign that the current build really
    /// applies the fake (the receiving side can only be checked on the other map).
    /// </summary>
    private static void Report(ZDO zdo, Vector3 fake)
    {
        if (s_reported.Add(zdo.m_uid))
        {
            Disguise.LogInfo(
                $"Map position spoof for ZDO {zdo.m_uid}: the other maps show ({fake.x:0.0}, {fake.z:0.0}) "
                    + "instead of your real position."
            );
        }
    }
}
