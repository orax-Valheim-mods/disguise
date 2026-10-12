#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Resolves a local player animation trigger into a trigger the disguised creature really
/// has (read by Patch_SetTrigger), and the block key into the creature's own third attack
/// (read by Patch_SetBool).
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
/// So an attack the creature does not know is broadcast a SECOND time, under the name of
/// one of the creature's own attacks: its NORMAL one for a normal swing, its SPECIAL one
/// for a secondary attack (middle click by default) - the two designated by CreatureAttacks
/// from the creature's weapon pool, with the normal one as the fallback when the pool holds
/// no special attack. The THIRD combat key has no trigger to bridge at all: the game
/// travels a block as the `blocking` BOOL (Humanoid.UpdateBlock), never as a SetTrigger,
/// so it is Patch_SetBool that watches it and comes here for the creature's middle attack
/// (ResolveBlock / TraceBlock).
///
/// Some of those names are viking names too (every player spear throws under
/// `spear_throw`, which is also the goblin's special attack), so the second name is
/// never applied on your own machine: Patch_RPC_SetTrigger drops it there, and only there,
/// while the clone - your own view - is fed directly by GhostAnimation. Nothing is
/// rewritten on reception: a machine rendering the creature plays the name it already has,
/// a machine rendering a viking ignores it, and no machine has to be patched.
///
/// Both reference sets are read from ACTIVE objects: Animator.parameters returns an empty
/// array while the animator is not active in the hierarchy, and a ZNetScene prefab never is
/// (m_prefabs holds assets, in no scene at all) - reading them from the prefabs is what
/// silently kept this bridge from ever sending anything. The player set therefore comes
/// from the live local player, the creature set from one throwaway activated copy per
/// creature (GhostFactory.BuildInert); the two attack ROLES come from the creature's
/// serialized weapon pool, which needs neither (CreatureAttacks).
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

    /// <summary>
    /// The three attack roles of the current creature, designated from its weapon pool
    /// (CreatureAttacks): what a normal swing plays, what a secondary attack plays and what
    /// the block key plays. The special one is null when the pool has only one attack -
    /// both attack keys then play the normal one, which is exactly what a creature with a
    /// single attack can do - and the block one is null as soon as the pool has no middle
    /// attack left to designate, which is most creatures (a third key without animation).
    /// </summary>
    private static string? s_normalAttack;

    private static string? s_specialAttack;

    private static string? s_blockAttack;

    private static HashSet<string> s_playerTriggers = new(StringComparer.Ordinal);

    private static bool s_playerTriggersRead;

    private static readonly Dictionary<string, string> s_traced = new(StringComparer.Ordinal);

    /// <summary>
    /// One log line per distinct outcome, only while disguised: what this bridge did with a
    /// trigger (or why it did nothing). The whole bridge runs on the disguised player's own
    /// client, so this line - in the log of that client - is the only place where its
    /// decision can be seen; section 7 of the README points at it. Repetitions of an
    /// outcome already reported are dropped, so a session stays at a handful of lines.
    /// </summary>
    internal static void Trace(string name, bool mine, string? resolved, string? kind)
    {
        if (mine && resolved == null && !LooksLikeAttack(name))
        {
            return; // ordinary non-attack with no counterpart: expected, nothing to report
        }
        string outcome;
        if (!mine)
        {
            outcome = "skipped: the component is not the local player's";
        }
        else if (resolved == null)
        {
            outcome =
                $"no creature equivalent (player {s_playerTriggers.Count}, creature {s_creatureTriggers.Count} triggers)";
        }
        else if (resolved == name)
        {
            outcome = "shared with the creature: nothing extra to send";
        }
        else
        {
            outcome =
                kind != null ? $"sent again as {resolved} ({kind})" : $"sent again as {resolved}";
        }
        Report(name, outcome);
    }

    /// <summary>
    /// The block key's own line of that same trace: there is no SOURCE name to report here -
    /// blocking travels as a bool and never reaches ZSyncAnimation.SetTrigger - so the
    /// outcome is written straight from what the weapon pool designated. "block" is only a
    /// label for the deduplication key, it is not a parameter name.
    /// </summary>
    internal static void TraceBlock(string? resolved)
    {
        Report(
            "block",
            resolved != null
                ? $"sent again as {resolved} (block attack)"
                : "no third attack (one or two attacks in the weapon pool, or none readable)"
        );
    }

    /// <summary>Writes one outcome per distinct name, so a session stays at a handful of lines.</summary>
    private static void Report(string name, string outcome)
    {
        if (s_traced.TryGetValue(name, out string? previous) && previous == outcome)
        {
            return;
        }
        s_traced[name] = outcome;
        Disguise.LogInfo($"Attack bridge: {name} -> {outcome}");
    }

    /// <summary>
    /// The trigger an object rendered as this ZDO's prefab must play (with the role it
    /// plays), or null when the creature has no such animation at all (nothing is sent,
    /// nothing is played - exactly what vanilla does with a name its animator ignores).
    /// </summary>
    internal static (string? trigger, string? kind) Resolve(ZDO? zdo, string name)
    {
        if (zdo == null || string.IsNullOrEmpty(name))
        {
            return (null, null);
        }
        int hash = zdo.GetPrefab();
        if (hash == 0 || hash == PrefabResolver.PlayerPrefabHash)
        {
            return (null, null); // not disguised: a vanilla machine renders a Player here
        }
        if (!ReadPlayerTriggers())
        {
            return (null, null); // no reference set yet: never risk a name the viking also knows
        }
        // Cheapest set first: the creature one costs an instantiation (see Probe).
        ReadCreatureTriggers(hash);
        if (s_creatureTriggers.Contains(name))
        {
            return (name, null); // already valid on the creature, nothing to bridge
        }
        if (!LooksLikeAttack(name))
        {
            return (null, null);
        }
        // Which of the two attacks the viking just used: a secondary attack (middle click
        // by default) plays the creature's special one, a normal swing its basic one - and
        // the basic one again when the pool holds no special attack (a boar, a wolf).
        if (IsSecondaryAttack(name) && s_specialAttack != null)
        {
            return (s_specialAttack, "special attack");
        }
        if (s_normalAttack != null)
        {
            return (s_normalAttack, "normal attack");
        }
        // No readable weapon pool (a prefab without Humanoid, an empty pool): the word match
        // of the original design, still restricted to names the viking does not know.
        string? companion = PickCompanion(name);
        return (companion, companion != null ? "word match" : null);
    }

    /// <summary>
    /// The creature's third attack, for the BLOCK key (right click by default): the middle
    /// attack of its weapon pool, or null when the pool has nothing left to designate (a
    /// pool of two attacks or fewer, or no readable pool - see CreatureAttacks).
    ///
    /// There is no source name to resolve here: a block never becomes a SetTrigger, it
    /// travels as the `blocking` bool, which is why Patch_SetBool asks for the creature's
    /// own attack directly. The name therefore already comes from the creature's controller,
    /// restricted to the triggers that controller really has - nothing to compare with
    /// Player_animator - and, unlike the two attack roles, it has no word-match fallback:
    /// when the pool designates nothing, the third key simply plays nothing.
    /// </summary>
    internal static string? ResolveBlock(ZDO? zdo)
    {
        if (zdo == null)
        {
            return null;
        }
        int hash = zdo.GetPrefab();
        if (hash == 0 || hash == PrefabResolver.PlayerPrefabHash)
        {
            return null; // not disguised: a vanilla machine renders a Player here
        }
        ReadCreatureTriggers(hash);
        return s_blockAttack;
    }

    /// <summary>
    /// True when the local player's current weapon produces <paramref name="name"/> with its
    /// SECONDARY attack (middle click by default). The name is compared with the animation
    /// the game itself starts (`Humanoid.StartAttack` picks m_secondaryAttack when
    /// secondaryAttack is set), suffix digits included: Attack.Start appends the chain level
    /// and the random variant to it. Reading it from the weapon is what covers the
    /// secondaries that are not
    /// called *_secondary at all - every player spear throws under `spear_throw`, the fists
    /// kick under `unarmed_kick` - while a weapon whose two clicks share one animation (the
    /// grappling hook) stays a normal attack.
    /// </summary>
    private static bool IsSecondaryAttack(string name)
    {
        Player? player = Player.m_localPlayer;
        ItemDrop.ItemData? weapon = player != null ? player.GetCurrentWeapon() : null;
        if (weapon == null || !weapon.HaveSecondaryAttack())
        {
            return false;
        }
        string secondary = weapon.m_shared.m_secondaryAttack.m_attackAnimation;
        if (
            string.IsNullOrEmpty(secondary)
            || (
                weapon.HavePrimaryAttack()
                && weapon.m_shared.m_attack.m_attackAnimation == secondary
            )
        )
        {
            return false;
        }
        int end = name.Length;
        while (end > 0 && char.IsDigit(name[end - 1]))
        {
            end--;
        }
        return end == secondary.Length && string.CompareOrdinal(name, 0, secondary, 0, end) == 0;
    }

    /// <summary>The creature's trigger list, read once per creature (see <see cref="Probe"/>).</summary>
    private static void ReadCreatureTriggers(int hash)
    {
        if (s_creatureHash == hash)
        {
            return;
        }
        s_creatureHash = hash;
        HashSet<string> triggers = Probe(hash);
        s_creatureTriggers = triggers;
        if (triggers.Count == 0)
        {
            s_normalAttack = null;
            s_specialAttack = null;
            s_blockAttack = null;
            s_creatureHash = 0; // prefab not resolvable yet: read it again on the next trigger
            return;
        }
        (s_normalAttack, s_specialAttack, s_blockAttack) = CreatureAttacks.Read(hash, triggers);
    }

    /// <summary>
    /// The trigger list of a creature, read from a throwaway ACTIVATED copy - the prefab
    /// itself cannot be read, because Animator.parameters returns an empty array while the
    /// animator is not active in the hierarchy and a ZNetScene prefab never is (that is what
    /// silently disabled this whole bridge before it was diagnosed). The copy is inert
    /// (GhostFactory has already stripped everything but the visuals), deactivated and
    /// destroyed before this returns, so one probe costs one instantiation per creature.
    /// </summary>
    private static HashSet<string> Probe(int hash)
    {
        GameObject? copy = GhostFactory.BuildInert(hash, keepEquipment: false, gearSeed: 0);
        if (copy == null)
        {
            return new(StringComparer.Ordinal);
        }
        try
        {
            copy.SetActive(true);
            return ReadTriggers(copy.GetComponentInChildren<Animator>(true));
        }
        finally
        {
            copy.SetActive(false); // never rendered: gone before the end of the frame
            UnityEngine.Object.Destroy(copy);
        }
    }

    /// <summary>
    /// Player_animator parameters, read once (same controller on every machine). Until that
    /// succeeds every Resolve bails out instead of guessing - the set is what guarantees the
    /// companion name can only ever be played by the creature.
    /// </summary>
    private static bool ReadPlayerTriggers()
    {
        if (s_playerTriggersRead)
        {
            return true;
        }
        // The live local player, not the prefab: an object that is not active in the
        // hierarchy reports no parameters at all (see Probe), and this is anyway the very
        // animator vanilla itself reads the 110 trigger names of Player_animator from.
        Animator? animator =
            Player.m_localPlayer != null
                ? Player.m_localPlayer.GetComponentInChildren<Animator>(true)
                : null;
        HashSet<string> triggers = ReadTriggers(animator);
        if (triggers.Count == 0)
        {
            return false;
        }
        s_playerTriggers = triggers;
        s_playerTriggersRead = true;
        return true;
    }

    private static HashSet<string> ReadTriggers(Animator? animator)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        if (animator == null)
        {
            return names;
        }
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (
                parameter.type == AnimatorControllerParameterType.Trigger
                && !string.IsNullOrEmpty(parameter.name)
            )
            {
                names.Add(parameter.name);
            }
        }
        return names;
    }

    /// <summary>
    /// Fallback, only when the weapon pool could not be read (a prefab without Humanoid, an
    /// empty pool): the creature's own attack the companion should play - a trigger it has,
    /// that the viking does not have (or the viking would swing twice), that reads as a melee
    /// attack, sharing the source's word when one does - ties are broken alphabetically so
    /// the choice never depends on the iteration order of the set.
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
