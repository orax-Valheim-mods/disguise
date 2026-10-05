#nullable enable
using HarmonyLib;

namespace Disguise;

/// <summary>
/// ZInput.GetMouseScrollWheel is the one call every part of the game uses to read the wheel:
/// the camera zoom (GameCamera.UpdateCamera), the minimap zoom and the build rotation.
///
/// Patch_UpdateCamera opens a window around the camera's own read, and this hands back 0
/// while that window is open - which cancels the zoom of exactly that frame, and nothing
/// else. The window is closed again before the next part of the game asks, so the minimap and
/// the build keep their wheel.
///
/// The climb never comes from here either: FlightControl.Tick reads the wheel on its own,
/// outside GameCamera.UpdateCamera, so it always gets the real value.
/// </summary>
[HarmonyPatch(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel))]
internal static class Patch_GetMouseScrollWheel
{
    private static void Postfix(ref float __result)
    {
        if (FlightControl.ZoomSuspended)
        {
            __result = 0f;
        }
    }
}
