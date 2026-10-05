#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Designates the three attacks a creature really has, read from its weapon pool: the
/// Humanoid of its prefab (m_defaultItems / m_randomWeapon / m_randomItems / m_randomSets /
/// m_unarmedWeapon), which is exactly the list MonsterAI.EquipBestWeapon equips from - a
/// creature attacks through the item it holds (Humanoid.StartAttack ->
/// ItemData.m_shared.m_attack), never through a component of its own.
///
/// Nothing in the game calls an attack "special", nor a third one "block": no creature item
/// carries a secondary attack, so the roles are read from the numbers the designer left on
/// those items:
///
///   NORMAL - the basic one: a melee primary first (a projectile ranks last), then the
///            SHORTEST m_aiAttackInterval (the AI comes back to it fastest), then the
///            pool order.
///   SPECIAL - the heaviest one: a secondary attack first if the pool has one, then the
///            LONGEST m_aiAttackInterval (the attack the AI waits longest for), and never
///            the same animation as the normal attack. Null when the pool holds a single
///            attack: a boar or a wolf simply has no special attack, and the bridge then
///            plays the normal one for both attack keys.
///   BLOCK - the middle one: what is left once the basic and the heaviest attacks are
///            spoken for, at its LOWER MEDIAN in the same light-to-heavy order, played by
///            the third combat key - block, right click by default (Patch_SetBool).
///            Null when nothing is left, i.e. a pool of two attacks or fewer, which is most
///            creatures: the third key then has no animation at all and the bridge stays
///            silent rather than repeat one of the two.
///
/// Measured on the real pools: Troll punch (4 s) / swing_logh (6 s) / ground slam (20 s),
/// Wolf attack1 / attack2 / attack3 (5 s each), Greydwarf jaws (2 s) vs thrown stone (8 s),
/// Goblin club (2 s) vs spear (4 s) - and for all of them the shortest cooldown is also
/// the plain melee swipe a disguise should show by default, while the middle one is a real
/// third animation of its own (a Troll swings its log sideways on the block key).
///
/// The prefab is read, never an instance: those are plain serialized fields, unlike
/// Animator.parameters which only exists on an object active in the hierarchy - so this
/// costs neither an activation nor an instantiation, on top of the one Probe already does.
/// </summary>
internal static class CreatureAttacks
{
    /// <summary>
    /// The creature's normal attack, special attack and block attack, restricted to the
    /// triggers its own controller really has (read by CreatureTriggers.Probe). Each role
    /// comes back null when the pool cannot fill it - a prefab without Humanoid (an animal
    /// without an inventory), an empty pool, a name the animator does not carry, or simply
    /// a third animation that does not exist - and the bridge then falls back to its word
    /// match (for the two attack roles) or plays nothing (for the block key).
    /// </summary>
    internal static (string? normal, string? special, string? block) Read(
        int prefabHash,
        HashSet<string> triggers
    )
    {
        if (triggers.Count == 0)
        {
            return (null, null, null);
        }
        GameObject? prefab = GhostFactory.Prefab(prefabHash);
        if (prefab == null)
        {
            return (null, null, null);
        }
        // One Humanoid per creature prefab, root or child; the inactive prefab answers
        // GetComponentInChildren like any other object.
        Humanoid? humanoid = prefab.GetComponentInChildren<Humanoid>(true);
        if (humanoid == null)
        {
            return (null, null, null);
        }

        // name / secondary / projectile / cooldown / pool order.
        List<(string name, bool secondary, bool projectile, float interval, int order)> entries =
            new();
        HashSet<string> seen = new(StringComparer.Ordinal);
        int order = 0;

        foreach (GameObject item in Pool(humanoid))
        {
            ItemDrop.ItemData? data = ItemDataOf(item);
            if (data != null)
            {
                Add(data, secondary: false);
                Add(data, secondary: true);
            }
        }

        if (entries.Count == 0)
        {
            return (null, null, null);
        }

        // The basic one. List.Sort is not stable, so the pool order is part of the key.
        entries.Sort(
            (a, b) =>
            {
                int rank = a.secondary.CompareTo(b.secondary);
                if (rank == 0)
                {
                    rank = a.projectile.CompareTo(b.projectile);
                }
                if (rank == 0)
                {
                    rank = a.interval.CompareTo(b.interval);
                }
                return rank != 0 ? rank : a.order.CompareTo(b.order);
            }
        );
        string normal = entries[0].name;

        // The heavy one: same entries, opposite priorities, and never the normal attack.
        List<(string name, bool secondary, bool projectile, float interval, int order)> heaviest =
            new(entries);
        heaviest.Sort(
            (a, b) =>
            {
                int rank = b.secondary.CompareTo(a.secondary);
                if (rank == 0)
                {
                    rank = b.interval.CompareTo(a.interval);
                }
                return rank != 0 ? rank : a.order.CompareTo(b.order);
            }
        );
        string? special = null;
        for (int i = 0; i < heaviest.Count; i++)
        {
            if (heaviest[i].name != normal)
            {
                special = heaviest[i].name;
                break;
            }
        }

        // The middle one, for the block key. `entries` is still ordered light-to-heavy (the
        // heavy copy above never touched it), so what is left after the basic and the
        // heaviest attacks are spoken for is taken at its LOWER MEDIAN - the middle entry
        // of an even count is the lighter of the two. Nothing left, that is a pool of two
        // attacks or fewer, means no third animation exists and the block key then plays
        // nothing instead of repeating one of the two.
        List<string> rest = new();
        for (int i = 0; i < entries.Count; i++)
        {
            string name = entries[i].name;
            if (name != normal && name != special)
            {
                rest.Add(name);
            }
        }
        string? block = rest.Count > 0 ? rest[(rest.Count - 1) / 2] : null;
        return (normal, special, block);

        // One entry per animation: the first item carrying it wins (the same animation on
        // a club, a sword and a torch is one attack, not three).
        void Add(ItemDrop.ItemData data, bool secondary)
        {
            Attack? attack = secondary ? data.m_shared.m_secondaryAttack : data.m_shared.m_attack;
            if (attack == null || string.IsNullOrEmpty(attack.m_attackAnimation))
            {
                return;
            }
            string name = attack.m_attackAnimation;
            if (!seen.Add(name) || !triggers.Contains(name))
            {
                return;
            }
            entries.Add(
                (
                    name,
                    secondary,
                    attack.m_attackType == Attack.AttackType.Projectile
                        || attack.m_attackType == Attack.AttackType.TriggerProjectile,
                    data.m_shared.m_aiAttackInterval,
                    order
                )
            );
            order++;
        }
    }

    /// <summary>
    /// The configured weapons of the creature, in the order the game itself would consider
    /// them (default items, then the random pools, then the sets, then the unarmed weapon).
    /// Armor is in there too and simply carries no attack.
    /// </summary>
    private static IEnumerable<GameObject> Pool(Humanoid humanoid)
    {
        foreach (GameObject item in humanoid.m_defaultItems ?? Array.Empty<GameObject>())
        {
            if (item != null)
            {
                yield return item;
            }
        }
        foreach (GameObject item in humanoid.m_randomWeapon ?? Array.Empty<GameObject>())
        {
            if (item != null)
            {
                yield return item;
            }
        }
        foreach (
            Humanoid.RandomItem item in humanoid.m_randomItems ?? Array.Empty<Humanoid.RandomItem>()
        )
        {
            if (item != null && item.m_prefab != null)
            {
                yield return item.m_prefab;
            }
        }
        foreach (Humanoid.ItemSet set in humanoid.m_randomSets ?? Array.Empty<Humanoid.ItemSet>())
        {
            if (set == null || set.m_items == null)
            {
                continue;
            }
            foreach (GameObject item in set.m_items)
            {
                if (item != null)
                {
                    yield return item;
                }
            }
        }
        if (humanoid.m_unarmedWeapon != null)
        {
            yield return humanoid.m_unarmedWeapon.gameObject;
        }
    }

    private static ItemDrop.ItemData? ItemDataOf(GameObject item)
    {
        ItemDrop? drop =
            item.GetComponent<ItemDrop>() ?? item.GetComponentInChildren<ItemDrop>(true);
        return drop != null ? drop.m_itemData : null;
    }
}
