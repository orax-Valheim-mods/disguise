#nullable enable
using System;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Builds the inert visual clone used by SelfView: the creature prefab reduced to its
/// visuals, with no ZDO, no AI and no collision.
///
/// The clone problem: instantiating an ACTIVE prefab runs every Awake inside Instantiate,
/// and ZNetView.Awake would immediately create a real ZDO for it - a phantom networked
/// object the whole server would see. The prefab is therefore deactivated for the duration
/// of the instantiation, which defers every Awake until SetActive(true): that gives us a
/// window to remove what we do not want and to activate the clone only once it is nothing
/// but visuals.
///
/// Only Transform, Animator, LODGroup, the renderers, the particle systems and the lights
/// survive - no Character, no Humanoid, no BaseAI, no ZNetView / ZSync*, no collider, no
/// rigidbody, no audio, no CharacterAnimEvent (its Awake needs a Character and its
/// callbacks drive game logic). The clone cannot move, cannot be seen by any game system
/// and never reaches the network.
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
    /// </summary>
    internal static GameObject? Create(int prefabHash, Vector3 position, Quaternion rotation)
    {
        ZNetScene? zns = ZNetScene.instance;
        GameObject? prefab = zns != null ? zns.GetPrefab(prefabHash) : null;
        if (prefab == null)
        {
            Disguise.LogWarning(
                $"Own view: prefab not found ({PrefabResolver.PrefabName(prefabHash)})."
            );
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

            clone.name = prefab.name + " (Disguise own view)";

            // The sync list has to be read here: ZSyncAnimation is removed by Strip below,
            // but its serialized fields are already populated on the inactive clone.
            GhostAnimation.Capture(
                clone.GetComponent<ZSyncAnimation>()
                    ?? clone.GetComponentInChildren<ZSyncAnimation>(true)
            );

            Strip(clone);
            if (!IsInert(clone))
            {
                UnityEngine.Object.Destroy(clone);
                return null;
            }

            clone.transform.SetPositionAndRotation(position, rotation);

            clone.SetActive(true);
            GhostAnimation.Bind(clone.GetComponentInChildren<Animator>(true));
            return clone;
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Own view: cannot build the clone of {prefab.name}: {ex}");
            if (clone != null)
            {
                UnityEngine.Object.Destroy(clone);
            }
            return null;
        }
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
    private static void Strip(GameObject clone)
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
                if (component == null || component is Transform || IsVisual(component))
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
    private static bool IsInert(GameObject clone)
    {
        foreach (Component component in clone.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component is Transform || IsVisual(component))
            {
                continue;
            }
            Disguise.LogError(
                $"Own view: {component.GetType().Name} survived the strip of {clone.name} - discarding it instead of activating a half-alive copy."
            );
            return false;
        }
        return true;
    }

    private static bool IsVisual(Component component)
    {
        return component is Animator
            || component is LODGroup
            || component is Renderer
            || component is ParticleSystem
            || component is Light;
    }
}
