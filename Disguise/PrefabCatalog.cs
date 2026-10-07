#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Disguise;

/// <summary>
/// The list behind "disguise list" and behind the window's list: every vanilla prefab you
/// can actually wear, sorted by name and tagged with exactly one PrefabKind.
///
/// The first tag of them all is Static - a prefab without ZSyncTransform, which a machine
/// running no DLL of ours draws once at its creation position and never moves again (see
/// PrefabKind): it is the difference between a costume that follows you and one that stays
/// where you put it.
///
/// Two names never enter: a MODDED one - the name is not in the game's own asset manifest,
/// which no mod ever writes to (VanillaPrefabs holds that test) - and an EFFECT one - the
/// name starts with "fx_", "sfx_" or "vfx_" (IsEffect: the sound and the particles a prefab
/// spawns, not an appearance). Both are cut at the top of the walk, before anything is
/// fetched or counted, so the counts and the names always describe the same set.
///
/// The two lists stay identical because they come out of this one class - the window only
/// adds its two filters on top (the text field, then the seven category checkboxes).
///
/// ZNetScene is the technical limit of any disguise - a vanilla client rebuilds your object
/// from ITS OWN prefab table, by hash, so only prefabs every installation already carries
/// can ever be shown. An asset that is not a network prefab (terrain, scene-only objects)
/// never can, whatever the config says.
/// </summary>
internal static class PrefabCatalog
{
    /// <summary>How many kinds PrefabKind holds: the size of every array indexed by it.</summary>
    internal const int KindCount = 7;

    /// <summary>
    /// The component that moves an object, looked up by NAME and never as a generic:
    /// GetComponent("ZSyncTransform") is Unity's own overload, answers the same thing on a
    /// prefab asset, and keeps audit_visibility.ps1 where it is - a typed
    /// GetComponent&lt;ZSyncTransform&gt;() would add one more game member to a count that
    /// has been documented as 82 since the first patch, for a test this answers as well.
    /// </summary>
    private const string SyncTransformComponent = "ZSyncTransform";

    /// <summary>
    /// The kind of a prefab: the first test that matches wins, in the order of PrefabKind.
    /// Static comes first and hangs on that one component: without it nothing on that machine
    /// will ever write a ZDO position into this object again, whatever the ZDO says.
    /// Every other component is read on the root, exactly like the Character test the list
    /// shipped with, so the Creatures checkbox keeps listing the very same names as before -
    /// a component on a child never qualified, and still does not.
    /// </summary>
    internal static PrefabKind KindOf(GameObject prefab)
    {
        if (prefab.GetComponent(SyncTransformComponent) == null)
        {
            return PrefabKind.Static;
        }
        if (prefab.GetComponent<Character>() != null)
        {
            return PrefabKind.Creature;
        }
        if (prefab.GetComponent<ItemDrop>() != null)
        {
            return PrefabKind.Item;
        }
        if (prefab.GetComponent<Piece>() != null)
        {
            return PrefabKind.Building;
        }
        if (
            prefab.GetComponent<TreeBase>() != null
            || prefab.GetComponent<Plant>() != null
            || prefab.GetComponent<Pickable>() != null
            || prefab.GetComponent<Destructible>() != null
            || prefab.GetComponent<MineRock>() != null
            || prefab.GetComponent<MineRock5>() != null
        )
        {
            return PrefabKind.Nature;
        }
        if (prefab.GetComponent<Ship>() != null)
        {
            return PrefabKind.Ship;
        }
        return PrefabKind.Other;
    }

    /// <summary>
    /// The kind of a prefab given its NAME, the form both lists store: Other when the name
    /// resolves to nothing (which the list itself never hands out).
    /// </summary>
    internal static PrefabKind KindOf(string prefabName)
    {
        ZNetScene? zns = ZNetScene.instance;
        GameObject? prefab = zns != null ? zns.GetPrefab(prefabName) : null;
        return prefab != null ? KindOf(prefab) : PrefabKind.Other;
    }

    /// <summary>
    /// Every vanilla prefab name matching the optional substring, sorted case-insensitively.
    /// shown is the checkbox state indexed by PrefabKind: null means "every kind", which is
    /// what the console asks for, and the window passes the seven flags of its own.
    /// </summary>
    internal static List<string> Names(string? filter, bool[]? shown)
    {
        List<string> names = new();
        Walk(filter, shown, names, null);
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    /// <summary>How many prefabs of each kind match the filter: the counts the console prints.</summary>
    internal static int[] Counts(string? filter)
    {
        int[] counts = new int[KindCount];
        Walk(filter, null, null, counts);
        return counts;
    }

    /// <summary>
    /// The plural, lowercase name of a kind ("creatures", "buildings"): the console's count
    /// line and, capitalised, the label of the window's checkbox.
    /// </summary>
    internal static string Label(PrefabKind kind)
    {
        switch (kind)
        {
            case PrefabKind.Static:
                return "static";
            case PrefabKind.Creature:
                return "creatures";
            case PrefabKind.Item:
                return "items";
            case PrefabKind.Building:
                return "buildings";
            case PrefabKind.Nature:
                return "nature";
            case PrefabKind.Ship:
                return "ships";
            default:
                return "other";
        }
    }

    /// <summary>
    /// True for an effect prefab: "fx_", "sfx_" and "vfx_" are the game's own name for the
    /// sound and the particles a prefab spawns rather than something to wear - 1516 of them
    /// (281 fx_, 748 sfx_, 487 vfx_). Every one of them is spelled in lowercase today; the
    /// comparison ignores case so a later rename still lands here.
    /// </summary>
    private static bool IsEffect(string name)
    {
        return name.StartsWith("fx_", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("sfx_", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("vfx_", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// One pass over the registered prefabs: adds the matching names (when a list is given)
    /// and/or counts every match by kind. A modded name and an effect name never enter
    /// either - both are cut before anything else, so the counts and the names agree on the
    /// same set. Both callers are one-shot (a console command, the opening of the window),
    /// so nothing is cached - a stale cache would list prefabs a modded world no longer has.
    /// </summary>
    private static void Walk(string? filter, bool[]? shown, List<string>? names, int[]? counts)
    {
        ZNetScene? zns = ZNetScene.instance;
        if (zns == null)
        {
            return;
        }
        foreach (string name in zns.GetPrefabNames())
        {
            if (filter != null && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }
            if (!VanillaPrefabs.Contains(name) || IsEffect(name))
            {
                continue;
            }
            GameObject? prefab = zns.GetPrefab(name);
            if (prefab == null)
            {
                continue;
            }
            PrefabKind kind = KindOf(prefab);
            if (counts != null)
            {
                counts[(int)kind]++;
            }
            if ((shown == null || shown[(int)kind]) && names != null)
            {
                names.Add(name);
            }
        }
    }
}
