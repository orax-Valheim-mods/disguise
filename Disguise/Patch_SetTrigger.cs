#nullable enable
using System;
using HarmonyLib;

namespace Disguise;

/// <summary>
/// ZSyncAnimation.SetTrigger broadcasts a raw parameter name to every machine, where it is
/// applied to the animator of the object built for that ZDO: a real Player on a machine
/// running this DLL, the creature on a vanilla machine (which never runs this patch).
///
/// Two things must follow a local trigger, and vanilla offers no hook for either:
///   1. your own screen: the local clone (SelfView) IS the creature and carries no ZNetView
///      at all (GhostFactory strips it), so no RPC ever reaches it - GhostAnimation only
///      replays the ZDO-driven parameters;
///   2. a vanilla machine: its creature animator silently ignores a viking trigger it does
///      not have (ZSyncAnimation.Awake sets logWarnings = false), so a goblin never plays
///      swing_axe0 / knife_stab1 / bow_fire - that is what the second broadcast is for.
///
/// The second broadcast carries the name the creature really has - its NORMAL attack for a
/// normal swing, its SPECIAL one for a secondary attack (CreatureTriggers reads both from
/// the creature's weapon pool) - and it never lands on your own viking: ZNetView routes an
/// Everybody RPC through HandleRoutedRPC before InvokeRPC returns, so the local half of that
/// second broadcast is dropped by Patch_RPC_SetTrigger (some creature attacks are viking
/// attacks too: every player spear throws under `spear_throw`, which is also the goblin's
/// special attack). Nothing is rewritten on reception anywhere else: a machine rendering the
/// creature plays the name it already has, a machine rendering a viking ignores it. Both
/// steps run only for the local player's own ZSyncAnimation while its ZDO carries the
/// disguise marker: creatures you own, other players' triggers and a dedicated server
/// (Player.m_localPlayer is null) go through untouched. The sending half is `Send` below,
/// reused by Patch_SetBool for the block key - a block has no SetTrigger of its own to
/// follow.
/// </summary>
[HarmonyPatch(typeof(ZSyncAnimation), nameof(ZSyncAnimation.SetTrigger))]
internal static class Patch_SetTrigger
{
    /// <summary>
    /// True only while our own companion RPC is in flight, and only on this machine: the
    /// local half of ZNetView.Everybody is delivered synchronously, inside InvokeRPC, so it
    /// is still true when it reaches RPC_SetTrigger - which Patch_RPC_SetTrigger reads.
    /// </summary>
    internal static bool s_applyingCompanion;

    private static void Postfix(ZSyncAnimation __instance, string name)
    {
        try
        {
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
            // The marker can only sit on our own player ZDO, so anything failing here is
            // worth one log line (see CreatureTriggers.Trace): it is the whole bridge.
            bool mine = __instance.GetComponent<Player>() == Player.m_localPlayer;
            string? creatureName = null;
            string? kind = null;
            if (mine)
            {
                (creatureName, kind) = CreatureTriggers.Resolve(zdo, name);
            }
            CreatureTriggers.Trace(name, mine, creatureName, kind);
            if (!mine || creatureName == null)
            {
                return; // no creature animation for this one: vanilla behaviour, no extra RPC
            }

            // Your own screen (the clone, which carries no RPC at all) and the wire: the
            // original name already went out through ZSyncAnimation.SetTrigger one line
            // above, so the companion is only for machines rendering the creature.
            Send(nview, creatureName, creatureName != name);
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Patch ZSyncAnimation.SetTrigger (postfix): {ex}");
        }
    }

    /// <summary>
    /// One resolved creature trigger, on the two screens it has to reach: the local clone
    /// first - it IS the creature and carries no ZNetView, so GhostAnimation is its only
    /// feed - then the wire, when the machines rendering the creature still have to be told.
    ///
    /// The local half of that Everybody RPC is dropped by Patch_RPC_SetTrigger, which reads
    /// s_applyingCompanion while it is still true (ZNetView delivers the local half
    /// synchronously, inside InvokeRPC), so the name can be one the viking knows too without
    /// your own body ever playing it. The attack path passes `broadcast: false` when the
    /// creature's own name was already sent by ZSyncAnimation.SetTrigger one line above;
    /// the block path has no original name at all and always broadcasts.
    /// </summary>
    internal static void Send(ZNetView? nview, string creatureName, bool broadcast)
    {
        GhostAnimation.SetTrigger(creatureName);
        if (!broadcast)
        {
            return;
        }
        s_applyingCompanion = true;
        try
        {
            nview?.InvokeRPC(ZNetView.Everybody, "SetTrigger", creatureName);
        }
        finally
        {
            s_applyingCompanion = false;
        }
    }
}
