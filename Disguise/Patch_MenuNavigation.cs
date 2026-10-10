#nullable enable
using HarmonyLib;

namespace Disguise;

/// <summary>
/// Menu.UpdateNavigation wires the gamepad's up / down chain over the buttons the menu KNOWS
/// about - and it runs every time the menu opens, and again whenever the manual-save cooldown
/// changes one of them. The Disguise entry is not one of those buttons, so this postfix puts
/// the two links it breaks back: the entry keeps the place it has in the list, after
/// Settings. Without it the entry is still clicked with the pointer, and only never reached
/// with a D-pad.
///
/// The method is private in the game: patched by NAME, which names it without referencing
/// any member of it - and nothing is read from the game here either, PauseMenuEntry hands
/// over the two buttons it already has.
/// </summary>
[HarmonyPatch(typeof(Menu), nameof(Menu.UpdateNavigation))]
internal static class Patch_MenuNavigation
{
    private static void Postfix() => PauseMenuEntry.WireNavigation();
}
