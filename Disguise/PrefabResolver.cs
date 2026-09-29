#nullable enable
using System;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Resolves and caches every hash the mod works with: the cd_disguise / cd_refresh marker
/// hashes, the vanilla 'Player' prefab hash and the configured creature prefab hash.
/// </summary>
internal static class PrefabResolver
{
    private static int s_playerPrefabHash;

    private static int s_creaturePrefabHash;

    private static int s_markerHash;

    private static int s_refreshHash;

    private static bool s_creatureWarningLogged;

    // ------------------------------------------------------------------ hashes / helpers

    internal static int MarkerHash
    {
        get
        {
            if (s_markerHash == 0)
            {
                s_markerHash = StringExtensionMethods.GetStableHashCode(Disguise.MarkerKey);
            }
            return s_markerHash;
        }
    }

    internal static int RefreshHash
    {
        get
        {
            if (s_refreshHash == 0)
            {
                s_refreshHash = StringExtensionMethods.GetStableHashCode(Disguise.RefreshKey);
            }
            return s_refreshHash;
        }
    }

    internal static int PlayerPrefabHash
    {
        get
        {
            if (s_playerPrefabHash != 0)
            {
                return s_playerPrefabHash;
            }
            ZNetScene? zns = ZNetScene.instance;
            if (zns == null)
            {
                return 0;
            }
            GameObject? prefab = Game.instance != null ? Game.instance.m_playerPrefab : null;
            if (prefab == null)
            {
                prefab = zns.GetPrefab("Player");
            }
            if (prefab == null)
            {
                return 0;
            }
            s_playerPrefabHash = zns.GetPrefabHash(prefab);
            return s_playerPrefabHash;
        }
    }

    internal static int CreaturePrefabHash
    {
        get
        {
            if (s_creaturePrefabHash != 0)
            {
                return s_creaturePrefabHash;
            }
            ZNetScene? zns = ZNetScene.instance;
            if (zns == null)
            {
                return 0;
            }
            string name = (DisguiseConfig.ConfigCreature.Value ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                return 0;
            }
            GameObject? prefab = zns.GetPrefab(name);
            if (prefab == null)
            {
                // case-insensitive lookup
                foreach (string candidate in zns.GetPrefabNames())
                {
                    if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
                    {
                        prefab = zns.GetPrefab(candidate);
                        break;
                    }
                }
            }
            if (prefab == null)
            {
                if (!s_creatureWarningLogged)
                {
                    s_creatureWarningLogged = true;
                    Disguise.LogWarning(
                        $"Creature prefab not found: \"{name}\". Console command: disguise list"
                    );
                }
                return 0;
            }
            s_creaturePrefabHash = zns.GetPrefabHash(prefab);
            return s_creaturePrefabHash;
        }
    }

    internal static void InvalidateCreatureHash()
    {
        s_creaturePrefabHash = 0;
        s_creatureWarningLogged = false;
        Disguise.s_loggedSummary = false;
        Disguise.LogInfo($"Configured creature: \"{DisguiseConfig.ConfigCreature.Value}\"");
    }

    /// <summary>Prefab our player ZDO should carry according to the configuration.</summary>
    internal static int DesiredPrefabHash()
    {
        if (DisguiseConfig.ConfigEnabled.Value)
        {
            int creature = CreaturePrefabHash;
            if (creature != 0)
            {
                return creature;
            }
        }
        return PlayerPrefabHash;
    }

    internal static string PrefabName(int hash)
    {
        if (hash == 0)
        {
            return "<none>";
        }
        ZNetScene? zns = ZNetScene.instance;
        if (zns == null)
        {
            return "#" + hash;
        }
        GameObject? go = zns.GetPrefab(hash);
        return go != null ? go.name : "#" + hash;
    }
}
