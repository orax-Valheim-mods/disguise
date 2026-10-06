#nullable enable
using HarmonyLib;

namespace Disguise;

/// <summary>
/// Player.TakeInput is the valve the whole game reads before acting on the player:
/// Player.Update skips movement, attacks and every use action when it returns false. It is
/// the same valve Valheim closes for its own panels (chat, console, inventory, and the
/// search field of the build menu), which is why a click in a menu never swings a weapon.
///
/// While the pointer is on the Disguise window (DisguiseGui.BlocksInput) the frame belongs
/// to the UI: a click on a button must not reach the character. Pointer anywhere else, and
/// the valve stays exactly as the game left it - the window is an overlay, not a pause, so
/// the player can still walk around with it open.
///
/// Nothing is called from here: the postfix only reads a boolean that DisguiseGui.Tick
/// computed for this frame, so it can never throw into Player.Update.
/// </summary>
[HarmonyPatch(typeof(Player), nameof(Player.TakeInput))]
internal static class Patch_TakeInput
{
    private static void Postfix(ref bool __result)
    {
        if (DisguiseGui.BlocksInput)
        {
            __result = false;
        }
    }
}
