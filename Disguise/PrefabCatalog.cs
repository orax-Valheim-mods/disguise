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
/// Three names never enter: a MODDED one - the name is not in the game's own asset manifest,
/// which no mod ever writes to (VanillaPrefabs holds that test) - an EFFECT one - the name
/// starts with "fx_", "sfx_" or "vfx_" (IsEffect: the sound and the particles a prefab
/// spawns, not an appearance) - and one with no MESH to be seen through (HasMesh: the
/// creature's own attack helpers, the zones a blob or Bonemass spawns, every object the game
/// uses rather than shows). The first two are cut at the top of the walk, before anything is
/// fetched or counted; the third is read on the prefab the walk has already fetched, so the
/// counts and the names still describe the same set.
///
/// The two lists stay identical because they come out of this one class - the window only
/// adds its two filters on top (the text field, then the checkbox row: six category boxes
/// and the Favorites box, which while it is ticked shows the marked names alone).
///
/// ZNetScene is the technical limit of any disguise - a vanilla client rebuilds your object
/// from ITS OWN prefab table, by hash, so only prefabs every installation already carries
/// can ever be shown. An asset that is not a network prefab (terrain, scene-only objects)
/// never can, whatever the config says.
/// </summary>
internal static class PrefabCatalog
{
    /// <summary>How many kinds PrefabKind holds: the size of every array indexed by it.</summary>
    internal const int KindCount = 6;

    /// <summary>
    /// The component that moves an object, looked up by NAME and never as a generic:
    /// GetComponent("ZSyncTransform") is Unity's own overload, answers the same thing on a
    /// prefab asset, and keeps audit_visibility.ps1 where it is - a typed
    /// GetComponent&lt;ZSyncTransform&gt;() would add one more game member to a count that
    /// has been documented as 82 since the first patch, for a test this answers as well.
    /// </summary>
    internal const string SyncTransformComponent = "ZSyncTransform";

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
    /// shown is a category mask indexed by PrefabKind, null meaning "every kind" - which is
    /// what BOTH callers pass, the console (its list always prints every category) and the
    /// window: the window takes every name and filters its OWN rows with it, so a category
    /// switched on later has its names here already instead of costing a second walk.
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
    /// Whether a costume could be DRAWN at all: one mesh renderer anywhere under the root - a
    /// MeshRenderer or a SkinnedMeshRenderer, the two types the preview counts when it frames
    /// its picture and warns "has no mesh to show" when it finds none of them
    /// (CreaturePreview.Frame). Particles and trails count on neither side: they are effects
    /// the costume spawns, not the costume.
    ///
    /// Read on the PREFAB ASSET, like every other test of this class - nothing is
    /// instantiated to find out - and every state counts, an inactive child and a switched-off
    /// renderer included: the asset ships the GEOMETRY, and geometry is what a costume needs
    /// at all. What a state like that costs at runtime is the preview's business, not the
    /// list's.
    ///
    /// This is the third name the two lists never show, and the reason is in the log: the
    /// creature's own attack helpers (Abomination_attack1, wraith_melee - registered Item
    /// prefabs the game draws for nobody), the zones a blob or Bonemass spawns, spawners and
    /// markers. They are objects the game USES rather than appearances to wear: the picture
    /// over there would be empty and the viking invisible in the world.
    /// </summary>
    private static bool HasMesh(GameObject prefab)
    {
        return prefab.GetComponentInChildren<MeshRenderer>(true) != null
            || prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
    }

    /// <summary>
    /// One pass over the registered prefabs: adds the matching names (when a list is given)
    /// and/or counts every match by kind. A modded name and an effect name never enter
    /// either - both are cut before anything is fetched - and neither does a prefab with no
    /// mesh to be seen through, cut here the moment it is fetched (HasMesh), so the counts
    /// and the names still agree on the same set. The kind is read ONLY when a caller asks
    /// for it (counts, or a category mask): KindOf is up to ten component tests, the half of
    /// this walk that costs, and a caller here for names alone - the window, which tags its
    /// rows itself - does not need them twice. Both callers are one-shot (a console command,
    /// the opening of the window), so nothing is cached - a stale cache would list prefabs a
    /// modded world no longer has.
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
            if (prefab == null || !HasMesh(prefab))
            {
                // No mesh, no costume: the picture would be empty and the viking invisible.
                continue;
            }
            if (counts == null && shown == null)
            {
                // Nobody here says WHICH kind this is: the names alone are enough, and the
                // window walks KindOf once, on the way to its rows.
                if (names != null)
                {
                    names.Add(name);
                }
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
