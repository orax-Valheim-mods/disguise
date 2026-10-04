#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Resolves a local player animation trigger into a trigger the disguised creature really
/// has (read by Patch_SetTrigger).
///
/// ZSyncAnimation.SetTrigger carries nothing in the ZDO: it broadcasts the raw parameter
/// NAME, and every machine applies it to the animator of the object IT built for that ZDO -
/// a real Player on a machine running this DLL, the creature on a vanilla machine
/// (Patch_CreateObject only exists here). A name that animator does not have is dropped
/// without a trace: ZSyncAnimation.Awake sets Animator.logWarnings = false.
///
/// That is why a viking disguised as a goblin never swings: goblin_animator knows exactly
/// five triggers (jump, spear_throw, stagger, swing_longsword, wakeup) while an axe sends
/// swing_axe0, a knife knife_stab1, a bow bow_fire - and Player_animator knows 110.
///
/// So an attack the creature does not know is broadcast a SECOND time, under a name taken
/// from the creature's own controller and picked so that Player_animator does not know it
/// either: a machine rendering a Player ignores it as silently as the vanilla machine
/// ignores the first name, and no machine has to be patched or rewrite anything on
/// reception. Your own character keeps its animation for the same reason - a trigger only
/// reaches the object it was broadcast for, and the second name only exists on the creature.
/// </summary>
internal static class CreatureTriggers
{
    /// <summary>
    /// Melee attack words, most specific first. The first one found in a name is the word a
    /// companion should share when several of the creature's triggers qualify; anything with
    /// no word from this list (jump, dodge, eat, interact, emote_*, death*, equip_*) is not
    /// an attack and is never translated.
    /// </summary>
    private static readonly string[] s_attackWords =
    {
        "swing",
        "slash",
        "stab",
        "poke",
        "thrust",
        "cleave",
        "smash",
        "bash",
        "punch",
        "kick",
        "slam",
        "swipe",
        "bite",
        "claw",
        "sword",
        "axe",
        "mace",
        "spear",
        "knife",
        "dual",
        "atgeir",
        "unarmed",
        "hit",
        "strike",
        "attack",
    };

    /// <summary>
    /// Names that must never become a creature attack: ranged attacks (a creature shooting
    /// your bow would be worse than a creature standing still), attack_abort, which is a
    /// cancellation and not a swing, and the two build-tool swings - only the Hammer, the Hoe
    /// and the Cultivator carry them (checked against every ItemDrop), and a goblin has no
    /// reason to swing a sword on every placed piece. Tested before s_attackWords, so
    /// attack_bolt, spear_throw and fishingrod_throw are rejected even though they contain a
    /// melee word.
    /// </summary>
    private static readonly string[] s_notAttackWords =
    {
        "abort",
        "throw",
        "shoot",
        "bow",
        "fire",
        "bolt",
        "cast",
        "staff",
        "spit",
        "hammer",
        "hoe",
    };

    private static int s_creatureHash;

    private static HashSet<string> s_creatureTriggers = new(StringComparer.Ordinal);

    private static HashSet<string> s_playerTriggers = new(StringComparer.Ordinal);

    private static bool s_playerTriggersRead;

    /// <summary>
    /// The trigger an object rendered as this ZDO's prefab must play, or null when the
    /// creature has no such animation at all (nothing is sent, nothing is played - exactly
    /// what vanilla does with a name its animator ignores).
    /// </summary>
    internal static string? Resolve(ZDO? zdo, string name)
    {
        if (zdo == null || string.IsNullOrEmpty(name))
        {
            return null;
        }
        int hash = zdo.GetPrefab();
        if (hash == 0 || hash == PrefabResolver.PlayerPrefabHash)
        {
            return null; // not disguised: a vanilla machine renders a Player here
        }
        ReadCreatureTriggers(hash);
        if (!ReadPlayerTriggers())
        {
            return null; // no reference set yet: never risk a name the viking also knows
        }
        if (s_creatureTriggers.Contains(name))
        {
            return name; // already valid on the creature, nothing to bridge
        }
        return LooksLikeAttack(name) ? PickCompanion(name) : null;
    }

    /// <summary>Trigger parameters of the prefab this hash points at, read in place.</summary>
    private static void ReadCreatureTriggers(int hash)
    {
        if (s_creatureHash == hash)
        {
            return;
        }
        s_creatureHash = hash;
        s_creatureTriggers = TriggersOf(hash);
        if (s_creatureTriggers.Count == 0)
        {
            s_creatureHash = 0; // prefab not resolvable yet: read it again on the next trigger
        }
    }

    /// <summary>
    /// Player_animator parameters, read once from the Player prefab (same controller on every
    /// machine). Until that succeeds every Resolve bails out instead of guessing.
    /// </summary>
    private static bool ReadPlayerTriggers()
    {
        if (s_playerTriggersRead)
        {
            return true;
        }
        HashSet<string> triggers = TriggersOf(PrefabResolver.PlayerPrefabHash);
        if (triggers.Count == 0)
        {
            return false;
        }
        s_playerTriggers = triggers;
        s_playerTriggersRead = true;
        return true;
    }

    private static HashSet<string> TriggersOf(int hash)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        ZNetScene? zns = ZNetScene.instance;
        GameObject? prefab = hash != 0 && zns != null ? zns.GetPrefab(hash) : null;
        Animator? animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
        if (animator == null)
        {
            return names;
        }
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger)
            {
                names.Add(parameter.name);
            }
        }
        return names;
    }

    /// <summary>
    /// The creature's own attack the companion should play: a trigger it has, that the viking
    /// does not have (or the viking would swing twice), that reads as a melee attack, sharing
    /// the source's word when one does - ties are broken alphabetically so the choice never
    /// depends on the iteration order of the set.
    /// </summary>
    private static string? PickCompanion(string source)
    {
        string word = FirstAttackWord(source) ?? string.Empty;
        string? best = null;
        int bestRank = int.MaxValue;
        foreach (string candidate in s_creatureTriggers)
        {
            if (s_playerTriggers.Contains(candidate) || !LooksLikeAttack(candidate))
            {
                continue;
            }
            int rank =
                word.Length > 0 && candidate.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0
                    ? 0
                    : 1;
            if (
                best == null
                || rank < bestRank
                || (rank == bestRank && string.CompareOrdinal(candidate, best) < 0)
            )
            {
                best = candidate;
                bestRank = rank;
            }
        }
        return best;
    }

    private static bool LooksLikeAttack(string name)
    {
        foreach (string word in s_notAttackWords)
        {
            if (name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }
        }
        return FirstAttackWord(name) != null;
    }

    private static string? FirstAttackWord(string name)
    {
        foreach (string word in s_attackWords)
        {
            if (name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return word;
            }
        }
        return null;
    }
}
