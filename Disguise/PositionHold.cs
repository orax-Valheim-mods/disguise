#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Server, outgoing packet only: relays the position of a disguised player inside a small
/// band around the one already sent, which is what stops the costume shivering on the
/// machines that only render it.
///
/// The position a vanilla client draws comes from the packet header alone (ZDOMan.SendZDOs
/// writes it through ZDO.GetPosition, the receiver applies it with ZDO.InternalSetPosition),
/// and the value behind it is the OWNER'S RIGIDBODY position: a few millimetres of physics
/// jitter (penetration recovery, contacts) changing at every tick, even with the character
/// standing still. ZSyncTransform.SyncPosition then chases that moving target with a 0.2
/// lerp on every DataRevision, and a costume - a cart, a ship, a piece, a dropped item: a
/// rigid mesh with no animation to hide it behind - shows the chase as a permanent shiver of
/// a few millimetres. Holding the relayed value inside the band freezes the target: the
/// receiving object lerps to it once and then stops moving at all. A character that really
/// moves leaves the band in a single packet and is relayed exactly as it is.
///
/// ZDO.GetPosition hands out a copy, so this writes nothing: the owner's ZDO, the server's
/// own state, the saves and the game logic all keep the real position - the same rule as the
/// flight raise (Patch_ZDOGetPosition). Patch_SendZDOs opens the window around one
/// ZDOMan.SendZDOs call; the observer refresh is excluded, its far-away position being the
/// very thing that has to reach the other machines.
/// </summary>
internal static class PositionHold
{
    /// <summary>
    /// Band in metres: wider than the shiver it swallows (a few millimetres), narrow enough
    /// to be invisible, and left in a single packet by a character that walks away - so it
    /// costs nothing while the player really moves.
    /// </summary>
    private const float Band = 0.01f;

    /// <summary>
    /// Last position relayed for each disguised player: one entry per character ever seen in
    /// this session. A stale entry after a world change cannot hurt - it is only reused while
    /// the real position is still within a centimetre of it, and replaced on the spot.
    /// </summary>
    private static readonly Dictionary<ZDOID, Vector3> s_held = new Dictionary<ZDOID, Vector3>();

    /// <summary>True while the positions this call asks for are landing in a packet.</summary>
    private static bool s_sending;

    /// <summary>
    /// Opens the window for one ZDOMan.SendZDOs call. Only the relaying machine opens it: a
    /// client sending a held position would have the server STORE it.
    /// </summary>
    internal static void BeginSend()
    {
        s_sending = ZNet.instance != null && ZNet.instance.IsServer();
    }

    /// <summary>Closes the window: called by the Postfix and by the Finalizer.</summary>
    internal static void EndSend()
    {
        s_sending = false;
    }

    /// <summary>
    /// Hands back the position this ZDO was last relayed with while the real one stays inside
    /// the band, and the real one (adopted as the new held value) as soon as it leaves it.
    /// No-op outside the window, for a viking, and while the observer refresh fakes its
    /// far-away position.
    /// </summary>
    internal static void Hold(ZDO zdo, ref Vector3 position)
    {
        if (!s_sending)
        {
            return;
        }
        try
        {
            if (zdo.GetPrefab() == PrefabResolver.PlayerPrefabHash)
            {
                return; // not disguised: relay the viking exactly as it is
            }
            if (ObserverRefresh.s_refreshZdo == zdo)
            {
                return; // the fake far-away position is what re-creates the instance
            }
            ZDOID id = zdo.m_uid;
            if (!s_held.TryGetValue(id, out Vector3 held))
            {
                s_held[id] = position;
                return;
            }
            if ((position - held).sqrMagnitude > Band * Band)
            {
                // The character really moved: follow it, holding the value just adopted.
                s_held[id] = position;
                return;
            }
            position = held;
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Position hold (outgoing position): {ex}");
        }
    }
}
