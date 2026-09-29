#nullable enable
using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Disguise;

[HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.FindSectorObjects))]
internal static class Patch_FindSectorObjects
{
    private static void Postfix(List<ZDO> sectorObjects)
    {
        try
        {
            if (!Disguise.s_inCreateDestroy || sectorObjects == null)
            {
                return;
            }
            ZDO? local = Disguise.GetLocalPlayerZDO();
            if (local == null || !local.IsValid() || sectorObjects.Contains(local))
            {
                return;
            }
            sectorObjects.Add(local);
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Patch FindSectorObjects: {ex}");
        }
    }
}
