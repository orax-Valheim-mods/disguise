#nullable enable
using System;
using HarmonyLib;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Blocking is not a trigger: Humanoid.UpdateBlock writes the `blocking` parameter into the
/// animator as a BOOL through ZSyncAnimation.SetBool the moment the block starts, and takes
/// it back the moment it stops - which is why ZSyncAnimation.SetTrigger never sees it, and
/// why Patch_SetTrigger alone leaves the third combat key (block, right click by default)
/// with no animation at all.
///
/// This is that key's hook, and it fires exactly once per block: UpdateBlock only calls
/// SetBool when `m_internalBlockingState` turns over, so every condition the game itself
/// wants for a block has already been applied (not mid-attack, not mid-dodge, not while
/// drawing a bow, not while auto-running, not staggering, an item able to block in hand) -
/// there is no input reading and no edge detection left to duplicate here.
///
/// What is sent is the creature's THIRD attack, the middle one of its weapon pool, the one
/// neither the normal key (left click) nor the special one (middle click) plays:
/// CreatureTriggers.ResolveBlock reads it and comes back null for a pool of two attacks or
/// fewer, which is most creatures - the third key then has no animation to play and the
/// block simply stays a block, traced as such.
///
/// Only `true` is watched, and only on your own player: the block of a creature you own,
/// another player's block and the ZDO-driven copy an observer applies are the same
/// untouched vanilla path as the rest of the bridge.
/// </summary>
[HarmonyPatch(
    typeof(ZSyncAnimation),
    nameof(ZSyncAnimation.SetBool),
    new[] { typeof(int), typeof(bool) }
)]
internal static class Patch_SetBool
{
    /// <summary>
    /// The hash of the `blocking` parameter, computed exactly the way the game computes its
    /// own copy of it (Humanoid.s_blocking = ZSyncAnimation.GetHash("blocking") =
    /// Animator.StringToHash): that field is private in the original assembly, and the hash
    /// is a pure function of the name, so nothing is guessed - GhostAnimation reads its own
    /// parameters the same way.
    /// </summary>
    private static readonly int s_blockingHash = Animator.StringToHash("blocking");

    private static void Postfix(ZSyncAnimation __instance, int hash, bool value)
    {
        try
        {
            // Cheapest filter first: SetBool runs for every synced bool of every animated
            // object in the world, and only one of them concerns this bridge.
            if (!value || hash != s_blockingHash)
            {
                return;
            }
            if (Player.m_localPlayer == null)
            {
                return;
            }
            ZNetView? nview = __instance.GetComponent<ZNetView>();
            ZDO? zdo = nview != null ? nview.GetZDO() : null;
            if (!DisguiseApplier.HasMarker(zdo))
            {
                return; // not disguised: vanilla behaviour, no reason to log anything
            }
            // The marker can only sit on our own player ZDO, but a creature we own has an
            // animator too: only ours drives the bridge.
            if (__instance.GetComponent<Player>() != Player.m_localPlayer)
            {
                return;
            }
            string? creatureName = CreatureTriggers.ResolveBlock(zdo);
            CreatureTriggers.TraceBlock(creatureName);
            if (creatureName == null)
            {
                return; // no third attack to play: the block stays a block
            }
            Patch_SetTrigger.Send(nview, creatureName, broadcast: true);
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Patch ZSyncAnimation.SetBool (postfix): {ex}");
        }
    }
}
