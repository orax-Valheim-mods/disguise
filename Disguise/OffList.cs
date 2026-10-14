#nullable enable
using System.Collections.Generic;

namespace Disguise;

/// <summary>
/// Off the player list: the other games never receive YOUR entry in the list the server
/// broadcasts (ZNet.SendPlayerList -> RPC_PlayerList).
///
/// That list is more than the F2 overlay (ConnectPanel): every client keeps it as ITS
/// m_players, which feeds the "Players" line and count of that overlay, the session player
/// list of the map menu (SessionPlayerList), the pins of the map (Minimap, through
/// GetOtherPublicPlayers) and GetNrOfPlayers itself. Leaving the entry out takes the player
/// out of all of them, on every other screen, while nothing of the game moves: the server
/// keeps its own full list (the short one only ever exists during the write), the ZDO
/// position, peer.m_refPos - which decides the ZDOs a peer receives - the save file and
/// the moderation of the server console all stay exactly where they were.
///
/// Same contract as cd_hide / cd_rel / cd_fake: the choice is the OWNER's, published in
/// its own player ZDO (cd_offlist, OffList.Publish) - only the machine playing can decide,
/// relaying machines only follow. A client publishing no DLL publishes nothing and stays on
/// the list; a server running no DLL has no patch to leave anything out; and vanilla
/// clients need none of this DLL - they only ever show what the server sends them. The
/// server writes ONE list for every joining game, so while this is on that list holds
/// every name but yours - your own F2 too, while you play as a client; the HOST's own
/// screen reads the server's list, which is whole again before anything can look at it,
/// a hiding host therefore still seeing everyone on it, itself included.
///
/// The removal runs in the Prefix of Patch_WritePlayerInfo, BEFORE FakePosition swaps any
/// position (removals shift every index after them), and both are undone right after the
/// write. The list being written IS m_players, so the server's own copy - and the count
/// WritePlayerInfo reads off the field rather than off the parameter - is whole again
/// before anything can ask either.
/// </summary>
internal static class OffList
{
    // -- OWNER: what this machine asks the server to do with its entry -------------------

    /// <summary>
    /// Writes the choice into the local player ZDO: 1 while the other games must not
    /// receive this player's entry, 0 while they must. The disabling value is an explicit
    /// 0 - never a removed key, because ZDO.Deserialize only adds/overwrites the keys it
    /// receives (ZDOExtraData.Add never deletes): a removed key would simply be absent from
    /// the payload and the server would keep leaving the player out forever. Set() bumps
    /// the DataRevision only when the value really changes, so publishing it on every
    /// reassert costs nothing.
    /// </summary>
    internal static void Publish(ZDO? zdo)
    {
        if (zdo == null || !zdo.IsValid())
        {
            return;
        }
        int enabled = DisguiseConfig.ConfigHideFromPlayerList.Value ? 1 : 0;
        if (zdo.GetInt(Disguise.HideListKey, 0) != enabled)
        {
            zdo.Set(Disguise.HideListKey, enabled);
        }
    }

    // -- SERVER: the entries removed from the list being written (always restored) --------

    /// <summary>The list being written (the entries go back into it, never lost).</summary>
    private static List<ZNet.PlayerInfo>? s_players;

    /// <summary>Indexes of the entries removed by the current call, and the entries themselves.</summary>
    private static readonly List<int> s_removed = new();

    private static readonly List<ZNet.PlayerInfo> s_saved = new();

    /// <summary>SERVER: players already reported once in the log (one line each).</summary>
    private static readonly HashSet<ZDOID> s_reported = new();

    /// <summary>
    /// SERVER: takes every player that published cd_offlist out of the list before
    /// WritePlayerInfo serializes it. The list holds structs, so the entry is taken out and
    /// kept whole; Restore puts every one of them back where it found it. Removals walk the
    /// list backwards, so the recorded indexes stay the ORIGINAL ones and re-inserting them
    /// smallest first restores the very order the server built.
    /// </summary>
    internal static void ApplyTo(List<ZNet.PlayerInfo> players)
    {
        Restore(); // defensive: removals are never nested, and never survive the write
        if (players == null || players.Count == 0 || ZDOMan.instance == null)
        {
            return;
        }
        s_players = players;
        for (int i = players.Count - 1; i >= 0; i--)
        {
            ZNet.PlayerInfo info = players[i];
            ZDO? zdo = ZDOMan.instance.GetZDO(info.m_characterID);
            if (zdo == null || !zdo.IsValid() || zdo.GetInt(Disguise.HideListKey, 0) != 1)
            {
                continue;
            }
            s_removed.Add(i);
            s_saved.Add(info);
            players.RemoveAt(i);
            Report(zdo);
        }
    }

    /// <summary>
    /// Puts every removed entry back. Called after the write and, through the finalizer,
    /// when the original method threw; it is idempotent, so the server's own list can never
    /// leave this call shorter than it was built. Restored smallest index first (the list
    /// was walked backwards), which rebuilds the original order exactly.
    /// </summary>
    internal static void Restore()
    {
        List<ZNet.PlayerInfo>? players = s_players;
        int count = s_removed.Count;
        s_players = null;
        if (players != null)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                int index = s_removed[i];
                if (index >= 0 && index <= players.Count)
                {
                    players.Insert(index, s_saved[i]);
                }
            }
        }
        s_removed.Clear();
        s_saved.Clear();
    }

    /// <summary>
    /// Server log, once per hidden player: the only sign that the current build really
    /// applies the hiding (the receiving side can only be checked on the other screen - the
    /// F2 overlay of a vanilla client, or its map menu's session list).
    /// </summary>
    private static void Report(ZDO zdo)
    {
        if (s_reported.Add(zdo.m_uid))
        {
            Disguise.LogInfo(
                $"Player-list hiding for ZDO {zdo.m_uid}: the other players no longer receive this "
                    + "entry (F2 overlay, session list, map pins)."
            );
        }
    }
}
