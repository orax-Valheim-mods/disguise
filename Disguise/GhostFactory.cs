#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Builds the inert visual clone used by SelfView: the creature prefab reduced to its
/// visuals, with no ZDO, no AI and no collision.
///
/// The same build is what makes CreatureTriggers able to read a creature's animator at all
/// (BuildInert): Animator.parameters returns an empty array while the animator is not
/// active in the hierarchy, and a ZNetScene prefab never is - one activated, throwaway copy
/// is the only way to get the list of triggers a creature really has.
///
/// The clone problem: instantiating an ACTIVE prefab runs every Awake inside Instantiate,
/// and ZNetView.Awake would immediately create a real ZDO for it - a phantom networked
/// object the whole server would see. The prefab is therefore deactivated for the duration
/// of the instantiation, which defers every Awake until SetActive(true): that gives us a
/// window to remove what we do not want and to activate the clone only once it is nothing
/// but visuals.
///
/// Only Transform, Animator, LODGroup, the mesh filters, the renderers, the particle systems
/// and the lights survive - no Character, no Humanoid, no BaseAI, no ZNetView / ZSync*, no
/// collider, no rigidbody, no audio, no CharacterAnimEvent (its Awake needs a Character and
/// its callbacks drive game logic). The clone cannot move, cannot be seen by any game system
/// and never reaches the network.
///
/// One thing a copy that is SHOWN keeps on top of that list: its VisEquipment, dressed by
/// GearSkin in the gear the creature is born with. That component needs a ZNetView above it
/// to run, so the copy also carries a GhostNetView - the game's type with both of its
/// messages hidden, i.e. a ZNetView that never mints the ZDO it would otherwise create -
/// and VisEquipment, finding no ZDO, draws the copy's own slots instead (its else branch).
/// The throwaway copy of CreatureTriggers keeps neither: it exists to read an Animator.
///
/// One thing a clone is then TAKEN off, when the config asks for it: the shimmer of an item
/// (HideItemSparkle - HideSparkles, the particle systems of an Item costume draw nothing).
/// </summary>
internal static class GhostFactory
{
    /// <summary>
    /// Returns an activated, stripped clone of the prefab at <paramref name="position"/> /
    /// <paramref name="rotation"/> (the very transform a vanilla client gives it: your own
    /// player transform), or null when the prefab cannot be resolved.
    ///
    /// The clone is deliberately NOT parented to your player: a child would be walked by
    /// every GetComponentsInChildren the game (and SelfView) runs on your player, on top of
    /// extending your hierarchy by a full creature. SelfView copies the transform instead.
    ///
    /// <paramref name="gearSeed"/> is the seed the armor and the weapon are rolled with
    /// (GearSkin.Seed of the ZDO this costume belongs to - the seed the game rolls that
    /// same costume with everywhere), so what this screen draws is what every other screen
    /// draws, modded or not.
    /// </summary>
    internal static GameObject? Create(
        int prefabHash,
        Vector3 position,
        Quaternion rotation,
        int gearSeed
    )
    {
        GameObject? prefab = Prefab(prefabHash);
        if (prefab == null)
        {
            return null;
        }
        // The sync list has to be read HERE: BuildInert strips ZSyncAnimation away. The
        // prefab's serialized lists are exactly what the copy carries - a plain field read,
        // unlike Animator.parameters below, which does not exist on an inactive object.
        GhostAnimation.Capture(
            prefab.GetComponent<ZSyncAnimation>()
                ?? prefab.GetComponentInChildren<ZSyncAnimation>(true)
        );

        GameObject? clone = BuildInert(prefabHash, keepEquipment: true, gearSeed);
        if (clone == null)
        {
            return null;
        }
        try
        {
            clone.transform.SetPositionAndRotation(position, rotation);
            clone.SetActive(true);
            GhostAnimation.Bind(clone.GetComponentInChildren<Animator>(true));
            // Dressed for its level once awake, the way the game dresses its own creatures:
            // LevelEffects.Start ran in the activation above, found no Character (the strip
            // removed it) and stood down, which leaves its field free for LevelSkin to lend
            // it the prefab's own.
            LevelSkin.Apply(clone, prefab, DisguiseConfig.ConfigCreatureLevel.Value);
            return clone;
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Own view: cannot build the clone of {clone.name}: {ex}");
            UnityEngine.Object.Destroy(clone);
            return null;
        }
    }

    /// <summary>
    /// An INACTIVE copy of the prefab, reduced to pure visuals: every non-visual component
    /// is removed while the copy is still inactive, so no Awake has run yet and activating
    /// it can only wake renderers, lights, particles, LODGroup and the Animator - never a
    /// ZNetView (which would mint a real ZDO), a Character or an AI.
    ///
    /// <paramref name="keepEquipment"/> is what a copy that is SHOWN passes (SelfView, the
    /// window's picture): its VisEquipment survives the strip and a GhostNetView is added
    /// next to it - the pair GearSkin dresses in the gear the creature is born with, and
    /// the only thing on the copy that has anything to run. The throwaway copy of
    /// CreatureTriggers passes false: nothing ever draws it, so it carries neither.
    /// <paramref name="gearSeed"/> is only what those two are dressed with: the ZDO's own
    /// seed for your costume, 0 for a picture (GearSkin then rolls by the prefab's name).
    ///
    /// The caller owns the result. SelfView activates and renders it; CreatureTriggers
    /// activates it only to read its Animator - Animator.parameters returns an empty array
    /// while the animator is not active in the hierarchy, and a ZNetScene prefab never is
    /// (m_prefabs holds asset references, in no scene at all) - and destroys it in the same
    /// call, once per creature.
    /// </summary>
    internal static GameObject? BuildInert(int prefabHash, bool keepEquipment, int gearSeed)
    {
        GameObject? prefab = Prefab(prefabHash);
        if (prefab == null)
        {
            return null;
        }

        GameObject? clone = null;
        try
        {
            bool wasActive = prefab.activeSelf;
            try
            {
                if (wasActive)
                {
                    prefab.SetActive(false);
                }
                clone = UnityEngine.Object.Instantiate(prefab);
            }
            finally
            {
                // Restored before anything else runs: the prefab is shared with every real
                // spawn of that creature in the world.
                if (wasActive)
                {
                    prefab.SetActive(true);
                }
            }

            clone.name = prefab.name + " (Disguise copy)";

            Strip(clone, keepEquipment);
            if (HideSparkle(prefab))
            {
                HideSparkles(clone);
            }
            if (!IsInert(clone, keepEquipment))
            {
                UnityEngine.Object.Destroy(clone);
                return null;
            }
            if (keepEquipment)
            {
                // The pair that draws the gear the creature is born with: the VisEquipment
                // the strip kept, and the ZNetView-without-a-ZDO it needs to hang from -
                // without one its m_nview is null and Start throws. The slots are written
                // while the copy is still inactive, so its own Start draws them the moment
                // it is activated; and nothing of this writes a single slot when
                // HideCreatureGear asks for a bare costume, which is then built exactly
                // like before the gear existed.
                _ = clone.AddComponent<GhostNetView>();
                if (!GearSkin.HideGear(prefab))
                {
                    GearSkin.Dress(clone, prefab, gearSeed);
                }
            }
            return clone;
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Cannot build an inert copy of {prefab.name}: {ex}");
            if (clone != null)
            {
                UnityEngine.Object.Destroy(clone);
            }
            return null;
        }
    }

    internal static GameObject? Prefab(int prefabHash)
    {
        ZNetScene? zns = ZNetScene.instance;
        GameObject? prefab = zns != null ? zns.GetPrefab(prefabHash) : null;
        if (prefab == null)
        {
            Disguise.LogWarning($"Prefab not found ({PrefabResolver.PrefabName(prefabHash)}).");
        }
        return prefab;
    }

    /// <summary>
    /// Whether a costume built from this prefab has to be drawn WITHOUT its sparkle
    /// (HideItemSparkle): asked at every build (BuildInert) and whenever the option moves
    /// under a clone already standing (SelfView, GuiPreview).
    ///
    /// An ITEM test and nothing else - the shimmer is the game's own dropped-item particle,
    /// the ParticleSystem every item prefab carries on its root (Wood, Stone, a coin), so a
    /// creature, a piece or a ship keeps every effect it has, whatever this option says. The
    /// option comes second on purpose: with it off, which is what it starts at, the whole
    /// answer is one field read and KindOf - up to ten component tests - never runs.
    /// </summary>
    internal static bool HideSparkle(GameObject? prefab)
    {
        return prefab != null
            && DisguiseConfig.ConfigHideItemSparkle.Value
            && PrefabCatalog.KindOf(prefab) == PrefabKind.Item;
    }

    /// <summary>
    /// The same answer for the two live clones, which hold a hash (SelfView) or a name
    /// (GuiPreview) instead of a prefab. Nothing is warned about here: a prefab that cannot
    /// be resolved carries no particle system either, and the build itself complains.
    /// </summary>
    internal static bool HideSparkle(int prefabHash)
    {
        ZNetScene? zns = ZNetScene.instance;
        return HideSparkle(zns != null ? zns.GetPrefab(prefabHash) : null);
    }

    internal static bool HideSparkle(string prefabName)
    {
        ZNetScene? zns = ZNetScene.instance;
        return HideSparkle(zns != null ? zns.GetPrefab(prefabName) : null);
    }

    /// <summary>Working buffer of HideSparkles (reused, never allocated per build).</summary>
    private static readonly List<ParticleSystemRenderer> s_sparkles = new();

    /// <summary>
    /// Switches the DRAWING of every particle system of the clone off - the renderer, not
    /// the object it sits on (that object is the root of an item, the costume itself, so
    /// switching it off would switch the costume off too) and not the system either (a
    /// stopped one starts again the moment its object is activated: playOnAwake is the
    /// prefab's own). The renderer is what a picture is made of, so nothing of the shimmer
    /// reaches the screen while the system keeps simulating invisibly - one switch, no
    /// structure touched, and the clone is still inactive here, so the state is set before
    /// the first frame it could be seen in. Nothing has to remember what was switched off:
    /// a later change of the option builds a new clone (SelfView, GuiPreview) rather than
    /// repairing this one, from the pristine prefab.
    /// </summary>
    private static void HideSparkles(GameObject clone)
    {
        s_sparkles.Clear();
        clone.GetComponentsInChildren(true, s_sparkles);
        foreach (ParticleSystemRenderer renderer in s_sparkles)
        {
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }
        s_sparkles.Clear();
    }

    /// <summary>The strip repeats while it still manages to destroy something (dependency chains).</summary>
    private const int MaxStripPasses = 10;

    /// <summary>
    /// Removes every component that is not pure visuals. The clone is still inactive.
    ///
    /// Several passes, in reverse attachment order, because Unity REFUSES to destroy a
    /// component another one still requires ("Can't remove Humanoid (Script) because
    /// CharacterDrop (Script) depends on it"). One surviving Humanoid is enough to turn the
    /// inert copy into a walking fault: its Awake runs on activation, the creature's own
    /// RandomIdle state calls IsTamed() on it every frame, EnemyHud.TestShow() throws on it
    /// from LateUpdate, and its OnDestroy throws on the stripped SEMan - so the dead entry is
    /// never removed from Character.s_characters and the exceptions outlive the clone:
    /// a health bar frozen on screen and the frame time to go with it.
    /// </summary>
    private static void Strip(GameObject clone, bool keepEquipment)
    {
        for (int pass = 0; pass < MaxStripPasses; pass++)
        {
            Component[] components = clone.GetComponentsInChildren<Component>(true);
            bool destroyedSomething = false;
            for (int i = components.Length - 1; i >= 0; i--)
            {
                Component component = components[i];
                // Transforms (the skeleton, the bone hierarchy) must stay: the skinned meshes
                // reference them. Everything else keeps only if it is on the visual list.
                if (
                    component == null
                    || component is Transform
                    || IsVisual(component, keepEquipment)
                )
                {
                    continue;
                }
                // DestroyImmediate, not Destroy: the clone is activated in this same call, and a
                // component scheduled for destruction at the end of the frame would still Awake.
                UnityEngine.Object.DestroyImmediate(component);
                // Only a component that is really gone counts as progress: a refusal (see
                // summary) leaves it in place, and the next pass tries again once whatever
                // required it has been removed.
                destroyedSomething |= component == null;
            }
            if (!destroyedSomething)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Belt and braces: anything left over that is not a transform or pure visuals would wake
    /// up as a live object on an inert copy, so the clone is discarded instead of activated -
    /// failing loudly once, with the type named, rather than throwing on every frame after.
    /// </summary>
    private static bool IsInert(GameObject clone, bool keepEquipment)
    {
        foreach (Component component in clone.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component is Transform || IsVisual(component, keepEquipment))
            {
                continue;
            }
            Disguise.LogError(
                $"Inert copy: {component.GetType().Name} survived the strip of {clone.name} - discarding it instead of activating a half-alive copy."
            );
            return false;
        }
        return true;
    }

    /// <summary>
    /// What the strip keeps as "pure visuals". MeshFilter is data, not logic: no Awake, no
    /// behaviour, nothing to run - but a MeshRenderer that loses it keeps NO mesh at all and
    /// draws nothing, which used to leave every non-creature costume invisible on the screen
    /// wearing it (pieces, items, ships, carts, and most static ones too), while a creature,
    /// drawn by a SkinnedMeshRenderer, never misses it at all.
    ///
    /// <paramref name="keepEquipment"/> adds the one component that draws without being a
    /// renderer AND has something to run: the copy's own VisEquipment, which GearSkin
    /// dresses and whose GhostNetView carries no ZDO (the copy's slots, never the network).
    /// Kept only for a copy that is shown, and by NAME like everything the audit counts.
    /// </summary>
    private static bool IsVisual(Component component, bool keepEquipment)
    {
        return component is Animator
            || component is LODGroup
            || component is Renderer
            || component is ParticleSystem
            || component is MeshFilter
            || component is Light
            // LevelEffects dresses a creature for its level - the skin of a starred one - and
            // can do nothing at all without a Character above it: no Awake to fear, no Update
            // to run. It is the one thing here that draws without being a renderer, so the
            // copy of a starred creature keeps the skin its level calls for (LevelSkin hands
            // it the level). By NAME, like everything the audit counts: an "is" test would be
            // one more game type in the number the README documents.
            || component.GetType().Name == "LevelEffects"
            || (keepEquipment && component.GetType().Name == "VisEquipment");
    }
}
