#nullable enable
using HarmonyLib;

namespace Disguise;

/// <summary>
/// Keeps the companion of the attack bridge (Patch_SetTrigger) off THIS machine.
///
/// ZNetView.InvokeRPC(Everybody, ...) does not simply forget its own peer: ZRoutedRpc routes
/// a target of Everybody through HandleRoutedRPC on the spot, synchronously, so the local
/// half of that second broadcast runs inside our own InvokeRPC call and applies the name to
/// the animator of the object built for our player ZDO - a viking. Some creature attacks are
/// viking attacks too (every player spear throws under `spear_throw`, which is the goblin's
/// special attack), so the viking would really animate them instead of its own swing.
///
/// The companion therefore only ever travels: machines rendering the creature receive it
/// over the network (their flag is never set), your own creature view is fed directly by
/// GhostAnimation, and the original name - the one the game itself broadcasts - passes
/// through untouched because the flag is only set around our own send.
/// </summary>
[HarmonyPatch(typeof(ZSyncAnimation), nameof(ZSyncAnimation.RPC_SetTrigger))]
internal static class Patch_RPC_SetTrigger
{
    private static bool Prefix()
    {
        return !Patch_SetTrigger.s_applyingCompanion;
    }
}
