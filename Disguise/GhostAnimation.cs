#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Animation of the local creature clone built by GhostFactory (see SelfView).
///
/// On the other machines the creature prefab carries its own ZSyncAnimation, which reads
/// the animation parameters from OUR player ZDO (keys salted with c_ZDOSalt). The sync list
/// of a creature and the one of a viking only partly match, which is why a vanilla client
/// usually sees a creature that moves but keeps its idle pose: reproducing that branch here
/// is what makes your own screen show exactly what they render.
///
/// The clone has no ZNetView - a network view would create and bind a real ZDO - so the
/// game's ZSyncAnimation cannot run on it (its Awake registers RPCs on that view). This
/// class is a faithful copy of the non-owner branch of ZSyncAnimation.SyncParameters, fed
/// with the same local player ZDO.
/// </summary>
internal static class GhostAnimation
{
    private static int[] s_boolHashes = Array.Empty<int>();

    private static int[] s_floatHashes = Array.Empty<int>();

    private static int[] s_intHashes = Array.Empty<int>();

    private static bool[] s_boolDefaults = Array.Empty<bool>();

    private static float[] s_floatDefaults = Array.Empty<float>();

    private static int[] s_intDefaults = Array.Empty<int>();

    private static bool s_smoothSpeeds;

    private static Animator? s_animator;

    // ZSyncAnimation.s_forwardSpeedID / s_sidewaySpeedID: same strings, same hash.
    private static readonly int s_forwardSpeedHash = Animator.StringToHash("forward_speed");

    private static readonly int s_sidewaySpeedHash = Animator.StringToHash("sideway_speed");

    /// <summary>
    /// Reads the creature's sync list from its ZSyncAnimation. Must run while the clone is
    /// still inactive: GhostFactory strips that component (its Awake needs a ZNetView)
    /// before the clone is activated, but the serialized lists are already populated.
    /// A prefab without ZSyncAnimation simply keeps its idle pose.
    /// </summary>
    internal static void Capture(ZSyncAnimation? source)
    {
        Reset();
        if (source == null)
        {
            return;
        }
        s_smoothSpeeds = source.m_smoothCharacterSpeeds;
        s_boolHashes = Hashes(source.m_syncBools);
        s_floatHashes = Hashes(source.m_syncFloats);
        s_intHashes = Hashes(source.m_syncInts);
    }

    /// <summary>
    /// Binds the clone's animator and freezes the parameter defaults: ZSyncAnimation does
    /// the same in its own Awake, and the clone never runs it.
    /// </summary>
    internal static void Bind(Animator? animator)
    {
        s_animator = animator;
        s_boolDefaults = new bool[s_boolHashes.Length];
        s_floatDefaults = new float[s_floatHashes.Length];
        s_intDefaults = new int[s_intHashes.Length];
        if (animator == null)
        {
            Disguise.LogWarning("Own view: the creature prefab has no Animator.");
            return;
        }
        animator.logWarnings = false;
        // Nothing is left on the clone to apply root motion (CharacterAnimEvent is gone):
        // without this the model could drift away from your player transform.
        animator.applyRootMotion = false;
        for (int i = 0; i < s_boolHashes.Length; i++)
        {
            s_boolDefaults[i] = animator.GetBool(s_boolHashes[i]);
        }
        for (int i = 0; i < s_floatHashes.Length; i++)
        {
            s_floatDefaults[i] = animator.GetFloat(s_floatHashes[i]);
        }
        for (int i = 0; i < s_intHashes.Length; i++)
        {
            s_intDefaults[i] = animator.GetInteger(s_intHashes[i]);
        }
    }

    /// <summary>Applies one frame of ZDO-driven animation to the clone.</summary>
    internal static void Apply(ZDO zdo)
    {
        Animator? animator = s_animator;
        if (animator == null || !animator)
        {
            return;
        }

        // ZSyncAnimation.c_ZDOSalt: a const, the compiler bakes its value into our IL, so no
        // reference to that (private) member survives into the produced DLL.
        int salt = ZSyncAnimation.c_ZDOSalt;

        for (int i = 0; i < s_boolHashes.Length; i++)
        {
            int hash = s_boolHashes[i];
            animator.SetBool(hash, zdo.GetInt(salt + hash, s_boolDefaults[i] ? 1 : 0) != 0);
        }
        for (int i = 0; i < s_floatHashes.Length; i++)
        {
            int hash = s_floatHashes[i];
            float value = zdo.GetFloat(salt + hash, s_floatDefaults[i]);
            if (s_smoothSpeeds && (hash == s_forwardSpeedHash || hash == s_sidewaySpeedHash))
            {
                animator.SetFloat(hash, value, 0.2f, Time.fixedDeltaTime);
            }
            else
            {
                animator.SetFloat(hash, value);
            }
        }
        for (int i = 0; i < s_intHashes.Length; i++)
        {
            int hash = s_intHashes[i];
            animator.SetInteger(hash, zdo.GetInt(salt + hash, s_intDefaults[i]));
        }
        animator.speed = zdo.GetFloat(ZDOVars.s_animationSpeed, 1f);
    }

    internal static void Reset()
    {
        s_animator = null;
        s_smoothSpeeds = false;
        s_boolHashes = Array.Empty<int>();
        s_floatHashes = Array.Empty<int>();
        s_intHashes = Array.Empty<int>();
        s_boolDefaults = Array.Empty<bool>();
        s_floatDefaults = Array.Empty<float>();
        s_intDefaults = Array.Empty<int>();
    }

    private static int[] Hashes(List<string>? names)
    {
        if (names == null || names.Count == 0)
        {
            return Array.Empty<int>();
        }
        int[] hashes = new int[names.Count];
        for (int i = 0; i < names.Count; i++)
        {
            hashes[i] = Animator.StringToHash(names[i]);
        }
        return hashes;
    }
}
