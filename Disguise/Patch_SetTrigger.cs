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
///      swing_axe0 / knife_stab1 / bow_fire - the second broadcast carries a name that
///      creature really has (CreatureTriggers).
///
/// The second broadcast is harmless everywhere else by construction: the companion name is
/// one Player_animator does not have, so machines rendering a viking drop it exactly as the
/// vanilla machine drops the first name, and nothing is rewritten on reception. Both steps
/// run only for the local player's own ZSyncAnimation while its ZDO carries the disguise
/// marker: creatures you own, other players' triggers and a dedicated server
/// (Player.m_localPlayer is null) go through untouched.
/// </summary>
[HarmonyPatch(typeof(ZSyncAnimation), nameof(ZSyncAnimation.SetTrigger))]
internal static class Patch_SetTrigger
{
    private static void Postfix(ZSyncAnimation __instance, string name)
    {
        try
        {
            if (
                Player.m_localPlayer == null
                || __instance.GetComponent<Player>() != Player.m_localPlayer
            )
            {
                return;
            }
            ZNetView? nview = __instance.GetComponent<ZNetView>();
            ZDO? zdo = nview != null ? nview.GetZDO() : null;
            if (!DisguiseApplier.HasMarker(zdo))
            {
                return;
            }
            string? creatureName = CreatureTriggers.Resolve(zdo, name);
            if (creatureName == null)
            {
                return; // no creature animation for this one: vanilla behaviour, no extra RPC
            }

            // Your own screen: the clone is the creature and has no RPC, it is fed directly.
            GhostAnimation.SetTrigger(creatureName);

            // The wire: ZSyncAnimation.SetTrigger already sent the original name one line
            // above, this is the companion for machines rendering the creature.
            if (creatureName != name)
            {
                nview?.InvokeRPC(ZNetView.Everybody, "SetTrigger", creatureName);
            }
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Patch ZSyncAnimation.SetTrigger (postfix): {ex}");
        }
    }
}
