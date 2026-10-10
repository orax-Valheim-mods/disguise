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

    private static FootStep? s_creatureFootStep;

    private static int s_creatureFootStepHash;

    // The size of the configured costume, and the hash it was measured for.
    private static int s_creatureSizeHash;

    private static Vector3 s_creatureSize;

    private static bool s_creatureSizeMeasured;

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
        s_creatureFootStepHash = 0;
        s_creatureFootStep = null;
        s_creatureSizeHash = 0;
        s_creatureSize = Vector3.zero;
        s_creatureSizeMeasured = false;
        s_creatureWarningLogged = false;
        Disguise.s_loggedSummary = false;
        Disguise.LogInfo($"Configured prefab: \"{DisguiseConfig.ConfigCreature.Value}\"");
    }

    /// <summary>
    /// FootStep of the configured creature prefab, or null when the creature has none. It is a
    /// prefab asset (never instantiated, Start never ran), but FindBestStepEffect only reads
    /// the m_effects list, so the component can be queried in place. Cached against the hash it
    /// was resolved from, cleared together with that hash.
    /// </summary>
    internal static FootStep? CreatureFootStep()
    {
        int hash = CreaturePrefabHash;
        if (hash == 0)
        {
            return null;
        }
        if (s_creatureFootStepHash == hash)
        {
            return s_creatureFootStep;
        }
        s_creatureFootStepHash = hash;
        s_creatureFootStep = null;
        ZNetScene? zns = ZNetScene.instance;
        GameObject? prefab = zns != null ? zns.GetPrefab(hash) : null;
        if (prefab != null)
        {
            s_creatureFootStep = prefab.GetComponentInChildren<FootStep>(true);
        }
        return s_creatureFootStep;
    }

    /// <summary>
    /// How big the configured costume is, in metres: width, height and depth of one box around
    /// every mesh the costume really draws - which is the rule the preview frames its picture
    /// with (CreaturePreview.DrawsMesh and DrawnBounds), so the line and the picture can never
    /// disagree: particles and trails are effects the costume spawns rather than the costume, a
    /// mesh on a GameObject the prefab leaves inactive is drawn by nothing, and a skinned mesh
    /// is measured from its POSE - the box saved with the prefab is the bind pose, and
    /// Abomination_ragdoll lies folded over half of what its own says.
    ///
    /// Measured on the PREFAB ASSET, never on an instance: a registered prefab keeps the local
    /// transforms it is shipped with, so its renderers report the box the machines will draw,
    /// and nothing is instantiated, posed or destroyed to find out. The box is taken where the
    /// machines will draw the costume - its root at the origin, which is where the preview's
    /// own copy stands (Show zeroes the root) - rather than where the authoring scene parked
    /// the asset: CookedEgg stands 50 m up with a spare mesh 50 m below itself, and a box
    /// measured in the scene's units called that egg 50 m tall, while its own space says what
    /// it is - 0.6 x 0.1 x 0.4 m. The root's SCALE is kept in, since it is part of the size:
    /// a deer's root stands at 100 and its mesh is a hundredth of the animal. One walk, then
    /// cached against the hash like the FootStep above - the window's status line asks for it
    /// every frame the window is open. Zero when the prefab is not in the scene yet (asked
    /// again next frame) or when it has no mesh at all, and the status line then leaves the
    /// size out rather than printing a row of zeros.
    /// </summary>
    internal static Vector3 CreatureSize()
    {
        int hash = CreaturePrefabHash;
        if (hash == 0)
        {
            return Vector3.zero;
        }
        if (s_creatureSizeMeasured && hash == s_creatureSizeHash)
        {
            return s_creatureSize;
        }
        s_creatureSizeHash = hash;
        s_creatureSize = Vector3.zero;
        s_creatureSizeMeasured = true;

        ZNetScene? zns = ZNetScene.instance;
        GameObject? prefab = zns != null ? zns.GetPrefab(hash) : null;
        if (prefab == null)
        {
            s_creatureSizeMeasured = false; // not loaded yet: the next call measures again
            return Vector3.zero;
        }

        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);

        // Into the costume's own space, root at the origin, its scale still in it: the box the
        // machines will draw wherever the asset happens to stand.
        Transform root = prefab.transform;
        Matrix4x4 own = Matrix4x4.Scale(root.localScale) * root.worldToLocalMatrix;
        Bounds box = default;
        bool any = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!CreaturePreview.DrawsMesh(renderer))
            {
                continue;
            }
            Bounds drawn = Push(own, CreaturePreview.DrawnBounds(renderer, out _));
            if (!any)
            {
                box = drawn;
                any = true;
            }
            else
            {
                box.Encapsulate(drawn);
            }
        }
        if (any)
        {
            s_creatureSize = box.size;
        }
        return s_creatureSize;
    }

    /// <summary>
    /// A box taken through a matrix, corner by corner: a box turned by a matrix is no longer
    /// axis aligned, and its eight corners are the whole of what has to fit around it.
    /// </summary>
    private static Bounds Push(Matrix4x4 matrix, Bounds bounds)
    {
        Vector3 low = bounds.min;
        Vector3 high = bounds.max;
        Bounds box = default;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = matrix.MultiplyPoint3x4(
                new Vector3(
                    (i & 1) == 0 ? low.x : high.x,
                    (i & 2) == 0 ? low.y : high.y,
                    (i & 4) == 0 ? low.z : high.z
                )
            );
            if (i == 0)
            {
                box = new Bounds(corner, Vector3.zero);
            }
            else
            {
                box.Encapsulate(corner);
            }
        }
        return box;
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
