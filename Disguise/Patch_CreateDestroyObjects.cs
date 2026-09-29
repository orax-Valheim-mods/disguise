#nullable enable
using System;
using HarmonyLib;

namespace Disguise;

/// <summary>
/// Safety net: on a machine with a local player (you), YOUR player object must never be
/// destroyed by the local create/destroy tick - Player.OnDestroy would null
/// Player.m_localPlayer and break everything. It is only ever out of its own active area
/// while an observer refresh relays a fake position, so this changes nothing normally.
/// Scoped to ZNetScene.CreateDestroyObjects: the network relay path is left untouched.
/// </summary>
[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.CreateDestroyObjects))]
internal static class Patch_CreateDestroyObjects
{
    /// <summary>
    /// True only while ZNetScene.CreateDestroyObjects runs. Read by
    /// Patch_FindSectorObjects, which must include the local player in that window.
    /// </summary>
    internal static bool s_inCreateDestroy;

    private static void Prefix()
    {
        s_inCreateDestroy = true;
    }

    private static void Postfix()
    {
        s_inCreateDestroy = false;
    }

    // Runs even if the original method throws: never leave the flag stuck.
    private static Exception? Finalizer(Exception? __exception)
    {
        s_inCreateDestroy = false;
        return __exception;
    }
}
