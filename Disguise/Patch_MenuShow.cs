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
/// The prefix reads two plain booleans and nothing else, so it can never throw into
/// Menu.Update. The POSTFIX on this same method is the other half of this mod's presence in
/// the pause menu: it keeps the Disguise entry in the list that was just opened
/// (PauseMenuEntry). Harmony runs a postfix even when a prefix stopped the method itself,
/// which here means the entry is also built - or re-wired - while the menu did not open at
/// all; that costs one null test and shows nothing, the menu being hidden.
/// </summary>
[HarmonyPatch(typeof(Menu), nameof(Menu.Show))]
internal static class Patch_MenuShow
{
    private static bool Prefix() => !(DisguiseGui.IsOpen || DisguiseGui.EscapedThisFrame);

    private static void Postfix(Menu __instance) => PauseMenuEntry.Add(__instance);
}
