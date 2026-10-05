#nullable enable
using System;
using HarmonyLib;

namespace Disguise;

/// <summary>
/// ZDO.Serialize, third window (server only): while the packet is written, the flight height
/// the owner published (Disguise.FlyKey) is added to position.y, so a vanilla client - the
/// machine that really draws your creature - receives it in the air. Your own player, the
/// server's own state and the save file keep the real, grounded position: this window only
/// rewrites what goes out, and only when this machine relays.
///
/// A separate class from Patch_ZDOSerialize and Patch_ZDOSerializeRelation on purpose: each
/// window then keeps its own __state, so an exception in one can never leave another open -
/// the three windows are independent and nest in any order.
/// </summary>
[HarmonyPatch(typeof(ZDO), nameof(ZDO.Serialize))]
internal static class Patch_ZDOSerializeFlight
{
    private static bool Prefix(ZDO __instance, out bool __state)
    {
        __state = false;
        try
        {
            __state = FlightControl.BeginSerialize(__instance);
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Patch ZDO.Serialize flight (prefix): {ex}");
        }
        return true;
    }

    // The finalizer runs even if the original method threw: a raised position must never
    // outlive the call it was opened for.
    private static Exception? Finalizer(Exception? __exception, bool __state)
    {
        if (__state)
        {
            FlightControl.EndSerialize();
        }
        return __exception;
    }
}
