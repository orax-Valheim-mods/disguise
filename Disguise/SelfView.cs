#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Disguise;

/// <summary>
/// "See your disguise on your own screen" (SeeYourDisguise option / "disguise view"):
/// your viking is substituted - renderers and cloth switched off - by an inert local clone of
/// the creature, so that your own screen renders one character: what a vanilla client renders.
///
/// Local only: the clone has no ZDO, no network traffic and no game logic (GhostFactory), it
/// sits at your player transform (copied every frame) and keeps your player object, your
/// camera and your controls untouched. Nothing on the server or on the other machines ever
/// learns about it.
///
/// Kept out of your player hierarchy on purpose: a child would be walked by every
/// GetComponentsInChildren the game runs on you (and by our own scan), extending your
/// hierarchy by a whole creature. The transform copy below costs a single call per frame.
/// </summary>
internal static class SelfView
{
    private static GameObject? s_ghost;

    private static int s_ghostPrefabHash;

    /// <summary>
    /// The level the clone was dressed for (LevelSkin, at its build): a level that moves
    /// while it stands reaches the screen through a rebuild, the same way the prefab hash
    /// above does - one int comparison per frame, and the frame the config moves on costs
    /// one new clone.
    /// </summary>
    private static int s_ghostLevel;

    /// <summary>
    /// What the clone was built WITH (the answer of HideSparkle for its prefab) and the
    /// option itself at that moment: the pair is how a value changed while the clone stands
    /// - typed into ConfigurationManager, written by the window's box - reaches a clone
    /// already on screen. Only the move of the OPTION is watched (one bool per frame): the
    /// costume cannot change its kind behind our back, and the expensive test - ten component
    /// runs - is then made once per change, on a change that can matter only for an item.
    /// </summary>
    private static bool s_ghostHideSparkle;

    private static bool s_ghostSparkleOption;

    /// <summary>
    /// The same pair for the gear of a humanoid costume (ShowArmor and the weapons pair): the
    /// look
    /// the clone was built with (GearSkin.Look for its prefab, its bits) and the
    /// options themselves at its build. Here a move of the OPTIONS can show whatever the
    /// costume is - a humanoid draws the armor it is born with and holds its own weapon (or
    /// yours, or nothing at all while ShowWeapons is off), anything else has no gear at all -
    /// so the expensive test (the humanoid's six
    /// lists) runs once per change, and never while the options stand still: three config
    /// reads per frame, two more than the sparkle pair above.
    /// </summary>
    private static int s_ghostLook;

    private static int s_ghostLookOption;

    /// <summary>Working buffers for the scan (reused, never allocated per frame).</summary>
    private static readonly List<Renderer> s_renderers = new();

    private static readonly List<Cloth> s_cloths = new();

    /// <summary>The visual components this class switched off, restored one by one by Remove().</summary>
    private static readonly List<Renderer> s_hiddenRenderers = new();

    private static readonly List<Cloth> s_hiddenCloths = new();

    /// <summary>
    /// How often your visual components are re-scanned. Nothing the game runs re-enables a
    /// component we switched off (Character.RPC_ResetCloth only ever touches an enabled one),
    /// so the per-frame work stays a single transform copy: a hierarchy walk every frame would
    /// cost you the FPS this option is supposed to be free of. A newly spawned equipment
    /// instance is hidden within this delay at most.
    /// </summary>
    private const float RendererScanPeriod = 0.1f;

    // A prefab that cannot be cloned must not be retried on every frame.
    private static int s_failedPrefabHash;

    private static float s_nextBuildAttempt;

    private static float s_nextRendererScan;

    /// <summary>Called once per frame from Disguise.Update (decision, clone, animation).</summary>
    internal static void Tick()
    {
        try
        {
            Player? player = Player.m_localPlayer;
            ZDO? zdo = GameAccess.GetLocalPlayerZDO();
            int wanted = 0;
            if (
                player != null
                && zdo != null
                && zdo.IsValid()
                && DisguiseConfig.ConfigSeeYourDisguise.Value
            )
            {
                int prefab = zdo.GetPrefab();
                // The very hash a vanilla client instantiates in your place.
                if (prefab != 0 && prefab != PrefabResolver.PlayerPrefabHash)
                {
                    wanted = prefab;
                }
            }

            if (wanted == 0)
            {
                Remove();
                return;
            }

            int level = DisguiseConfig.ConfigCreatureLevel.Value;

            if (s_ghost != null && (s_ghostPrefabHash != wanted || s_ghostLevel != level))
            {
                Remove(); // the disguise or its level changed: drop the previous creature
            }

            // The sparkle option moved under a clone that is already standing: it was built
            // the old way, so it is built again - which only costs anything when the
            // difference can show, i.e. for an item (HideSparkle). With the option standing
            // still this is one bool, and with a creature costume the answer is false twice,
            // whatever the option says.
            bool sparkleOption = DisguiseConfig.ConfigHideItemSparkle.Value;
            if (s_ghost != null && sparkleOption != s_ghostSparkleOption)
            {
                s_ghostSparkleOption = sparkleOption;
                if (s_ghostHideSparkle != GhostFactory.HideSparkle(wanted))
                {
                    Remove(); // rebuilt by the block below, within this same frame
                }
            }

            // The same thing for the gear options (ShowArmor and the weapons pair): the clone
            // standing was built the other way, so it is built again - which only costs
            // anything when the difference can show: the bits are compared first, and
            // the humanoid's lists are only walked while the armor is on (GearSkin.Look).
            int lookOption = GearSkin.Options();
            if (s_ghost != null && lookOption != s_ghostLookOption)
            {
                s_ghostLookOption = lookOption;
                if (s_ghostLook != GearSkin.Look(wanted))
                {
                    Remove(); // rebuilt by the block below, within this same frame
                }
            }

            if (s_ghost == null)
            {
                if (s_failedPrefabHash == wanted && Time.unscaledTime < s_nextBuildAttempt)
                {
                    return;
                }
                Vector3 basePosition = player!.transform.position;
                GameObject? ghost = GhostFactory.Create(
                    wanted,
                    basePosition
                        + RestOffset.ViewOffset(wanted, basePosition)
                        + PoseControl.ViewOffset
                        + FlightControl.ViewOffset,
                    RestOffset.ViewRotation(wanted, basePosition, player.transform.rotation)
                        * PoseControl.ViewRotation,
                    // The ZDO's own seed: what YOUR costume is rolled with everywhere - the
                    // same armor and the same weapon the machines running no DLL receive
                    // (CostumeGear) and draw on it.
                    GearSkin.Seed(GameAccess.GetLocalPlayerZDO())
                );
                if (ghost == null)
                {
                    s_failedPrefabHash = wanted;
                    s_nextBuildAttempt = Time.unscaledTime + 5f;
                    return;
                }
                s_ghost = ghost;
                s_ghostPrefabHash = wanted;
                s_ghostHideSparkle = GhostFactory.HideSparkle(wanted);
                s_ghostSparkleOption = DisguiseConfig.ConfigHideItemSparkle.Value;
                s_ghostLook = GearSkin.Look(wanted);
                s_ghostLookOption = GearSkin.Options();
                s_ghostLevel = level;
                s_failedPrefabHash = 0;
                s_nextRendererScan = 0f;
                Disguise.LogInfo(
                    $"Own view: rendering {PrefabResolver.PrefabName(wanted)} in place of your player."
                );
            }

            GhostAnimation.Apply(zdo!);
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Own view: {ex}");
            Remove();
        }
    }

    /// <summary>
    /// Called once per frame from Disguise.LateUpdate: every game script has moved the player
    /// by then, so the clone lands exactly on it before rendering, and the (rare) renderer
    /// scan sees whatever this frame instantiated.
    /// </summary>
    internal static void LateTick()
    {
        try
        {
            if (s_ghost == null)
            {
                return;
            }

            Player? player = Player.m_localPlayer;
            if (player == null)
            {
                return; // Tick() frees everything on its next call
            }

            Vector3 basePosition = player.transform.position;
            s_ghost.transform.SetPositionAndRotation(
                basePosition
                    + RestOffset.ViewOffset(s_ghostPrefabHash, basePosition)
                    + PoseControl.ViewOffset
                    + FlightControl.ViewOffset,
                RestOffset.ViewRotation(s_ghostPrefabHash, basePosition, player.transform.rotation)
                    * PoseControl.ViewRotation
            );

            if (Time.unscaledTime >= s_nextRendererScan)
            {
                s_nextRendererScan = Time.unscaledTime + RendererScanPeriod;
                HideOwnVisuals(player);
            }
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Own view: {ex}");
            Remove();
        }
    }

    /// <summary>Frees everything: called on shutdown, after the normal view is restored.</summary>
    internal static void Shutdown()
    {
        Remove();
        s_failedPrefabHash = 0;
        s_nextBuildAttempt = 0f;
    }

    /// <summary>
    /// Substitutes your own viking: body, hair and equipment renderers plus its cloth are all
    /// switched off, so your own screen renders one character - the clone - instead of one
    /// and a half. That is also what the other machines receive: they never get your player
    /// object, only the creature. The clone itself is never touched, and a component that was
    /// already off is left alone so it is handed back exactly as found.
    ///
    /// Particle systems need no entry here: their renderer is one, so the scan above already
    /// stops their drawing, and their simulation is not ours to start and stop. Runs a few
    /// times per second (RendererScanPeriod), never per frame.
    /// </summary>
    private static void HideOwnVisuals(Player player)
    {
        Transform? ghost = s_ghost != null ? s_ghost.transform : null;

        s_renderers.Clear();
        player.GetComponentsInChildren(true, s_renderers);
        foreach (Renderer renderer in s_renderers)
        {
            if (renderer == null || !renderer.enabled || IsInClone(renderer, ghost))
            {
                continue;
            }
            if (!s_hiddenRenderers.Contains(renderer))
            {
                s_hiddenRenderers.Add(renderer);
            }
            renderer.enabled = false;
        }

        s_cloths.Clear();
        player.GetComponentsInChildren(true, s_cloths);
        foreach (Cloth cloth in s_cloths)
        {
            if (cloth == null || !cloth.enabled || IsInClone(cloth, ghost))
            {
                continue;
            }
            if (!s_hiddenCloths.Contains(cloth))
            {
                s_hiddenCloths.Add(cloth);
            }
            cloth.enabled = false;
        }
    }

    /// <summary>
    /// Two loops instead of one generic method: Renderer and Cloth both derive from Component
    /// (not Behaviour), so their common "enabled" switch has no base class to reach through.
    /// </summary>
    private static bool IsInClone(Component component, Transform? ghost)
    {
        return ghost != null && component.transform.IsChildOf(ghost);
    }

    private static void Remove()
    {
        foreach (Renderer renderer in s_hiddenRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }
        s_hiddenRenderers.Clear();
        s_renderers.Clear();

        foreach (Cloth cloth in s_hiddenCloths)
        {
            if (cloth != null)
            {
                cloth.enabled = true;
            }
        }
        s_hiddenCloths.Clear();
        s_cloths.Clear();

        if (s_ghost != null)
        {
            UnityEngine.Object.Destroy(s_ghost);
        }
        s_ghost = null;
        s_ghostPrefabHash = 0;
        s_ghostHideSparkle = false;
        s_ghostSparkleOption = false;
        s_ghostLook = 0;
        s_ghostLookOption = 0;
        s_ghostLevel = 0;
        s_nextRendererScan = 0f;
        GhostAnimation.Reset();
    }
}
