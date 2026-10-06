#nullable enable
using HarmonyLib;

namespace Disguise;

/// <summary>
/// Menu.Show opens the pause menu, and the only caller is the Escape branch of Menu.Update:
/// it pauses the game (Game.Pause), hides the chat and logs a Gogan event, so letting it run
/// on the press that closes the Disguise window would leave the game paused behind a window
/// that is already gone.
///
/// Both Updates run in the same frame, in an order nothing pins down, hence the two halves of
/// the condition: IsOpen when Menu.Update comes first (the window is still on screen), and
/// EscapedThisFrame when Tick comes first (the window closed a moment ago). The flag belongs
/// to one frame only, so the NEXT Escape opens the pause menu as usual.
///
/// Nothing is called from here: the prefix only reads two plain booleans, so it can never
/// throw into Menu.Update.
/// </summary>
[HarmonyPatch(typeof(Menu), nameof(Menu.Show))]
internal static class Patch_MenuShow
{
    private static bool Prefix() => !(DisguiseGui.IsOpen || DisguiseGui.EscapedThisFrame);
}
