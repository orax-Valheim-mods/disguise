#nullable enable
using System;
using HarmonyLib;

namespace Disguise;

/// <summary>
/// GameCamera.UpdateCamera reads the scroll wheel once and writes ONLY GameCamera.m_distance
/// with it: that write IS the camera zoom of the frame. While the flight owns the wheel
/// (FlightControl.ScrollTakesOver), the scroll read inside this call is therefore made worth
/// nothing (Patch_GetMouseScrollWheel), and the zoom comes out of the call unchanged.
///
/// The window (any machine that renders) is opened by the Prefix and closed by the Finalizer,
/// which runs even if the camera threw: a swallowed wheel must never outlive this call, or
/// the next minimap zoom or build rotation would be lost.
///
/// Nothing else reads the wheel inside this call, so the minimap, the build rotation and the
/// climb itself - read by FlightControl.Tick, outside it - are untouched.
/// </summary>
[HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateCamera))]
internal static class Patch_UpdateCamera
{
    private static void Prefix()
    {
        try
        {
            FlightControl.SuspendZoom();
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Patch UpdateCamera (prefix): {ex}");
        }
    }

    private static Exception? Finalizer(Exception? __exception)
    {
        try
        {
            FlightControl.ResumeZoom();
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Patch UpdateCamera (finalizer): {ex}");
        }
        return __exception;
    }
}
