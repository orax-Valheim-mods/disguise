#nullable enable
using HarmonyLib;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Player.SetMouseLook is where every turn of the camera arrives: PlayerController.LateUpdate
/// hands it the mouse delta (scaled by the sensitivity), Player.UpdateGyro the controller.
/// Zero is exactly what Valheim itself passes when the look does not belong to the player
/// (inventory open, build menu search focused, character in a cutscene).
///
/// While the pointer is on the Disguise window the mouse belongs to the UI
/// (DisguiseGui.BlocksInput), so the camera waits instead of spinning behind the window.
/// Pointer anywhere else, the look is untouched: with the window open the game keeps
/// running, which is what makes it an overlay.
///
/// Nothing is called from here: the prefix only reads a boolean that DisguiseGui.Tick
/// computed for this frame, so it can never throw into PlayerController.LateUpdate.
/// </summary>
[HarmonyPatch(typeof(Player), nameof(Player.SetMouseLook))]
internal static class Patch_SetMouseLook
{
    private static void Prefix(ref Vector2 mouseLook)
    {
        if (DisguiseGui.BlocksInput)
        {
            mouseLook = Vector2.zero;
        }
    }
}
