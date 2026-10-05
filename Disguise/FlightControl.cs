#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Visual flight of the creature: the local state (flying or not, height above the ground),
/// the four configurable key chords, and the two places that height is rendered.
///
/// Your player NEVER moves. The real ZDO position - what the server stores in the save, runs
/// the AI and the combat with - stays on the ground; only what the other machines DRAW is
/// raised:
///  - your own screen: SelfView adds ViewOffset to the creature clone (SeeYourDisguise);
///  - every other machine: while the relaying machine builds an outgoing packet, the height
///    published in cd_fly is added to the position that packet carries for that ZDO
///    (Patch_ZDOGetPosition, for the duration of that one call).
///
/// A position travels on the wire in exactly ONE place: the per-ZDO header that
/// ZDOMan.SendZDOs writes through ZDO.GetPosition, which the receiver applies with
/// ZDO.InternalSetPosition before it deserializes the rest of the ZDO. ZDO.Deserialize never
/// reads a position - the only other writer of one, ZDO.Save, is the save file - so raising
/// it inside ZDO.Serialize (the body) changes nothing anybody reads. The raise therefore
/// hands back a raised COPY and mutates nothing: the server's own state, the saves and the
/// game logic keep the grounded player, and there is no fake position anywhere to restore.
///
/// That window is opened only when this machine IS the server - a client sending a raised
/// position would have the server store it - and only for a ZDO whose prefab is NOT the
/// player one, so a viking is never drawn in the air. Others therefore see the flight only
/// when this DLL runs on the server (dedicated server, or your own PC hosting the world).
/// </summary>
internal static class FlightControl
{
    /// <summary>Height the creature rises to when the flight starts (metres).</summary>
    private const float StartHeight = 2f;

    /// <summary>Farthest point above the ground, to stay inside the world (metres).</summary>
    private const float MaxHeight = 100f;

    /// <summary>Smallest gap between two publications of the height (seconds).</summary>
    private const float PublishInterval = 0.1f;

    private static KeyCode[] s_enableChord = Array.Empty<KeyCode>();
    private static KeyCode[] s_disableChord = Array.Empty<KeyCode>();
    private static KeyCode[] s_upChord = Array.Empty<KeyCode>();
    private static KeyCode[] s_downChord = Array.Empty<KeyCode>();
    private static bool s_chordsLoaded;

    private static bool s_flying;
    private static float s_height;
    private static float s_nextPublishTime;

    // Outgoing-packet window (server only): true exactly while ZDOMan.SendZDOs is building
    // the packet, the only place a position leaves this machine. Patch_SendZDOs opens it.
    private static bool s_sending;

    /// <summary>Whether the flight is on (the height can still be 0: flying at ground level).</summary>
    internal static bool IsFlying => s_flying;

    /// <summary>Height above the player, in metres.</summary>
    internal static float Height => s_height;

    /// <summary>What the local creature clone adds to the player position, every frame.</summary>
    internal static Vector3 ViewOffset => Vector3.up * s_height;

    // ------------------------------------------------------------------ keys and state

    /// <summary>Re-reads the four chords: called from Disguise.Update, once per frame.</summary>
    internal static void Tick()
    {
        try
        {
            if (!s_chordsLoaded)
            {
                s_chordsLoaded = true;
                s_enableChord = ParseChord("FlyEnableKey", DisguiseConfig.ConfigFlyEnableKey.Value);
                s_disableChord = ParseChord(
                    "FlyDisableKey",
                    DisguiseConfig.ConfigFlyDisableKey.Value
                );
                s_upChord = ParseChord("FlyUpKey", DisguiseConfig.ConfigFlyUpKey.Value);
                s_downChord = ParseChord("FlyDownKey", DisguiseConfig.ConfigFlyDownKey.Value);
            }

            if (Player.m_localPlayer == null)
            {
                return;
            }

            if (ChordPressed(s_enableChord))
            {
                Enable();
            }
            if (ChordPressed(s_disableChord))
            {
                Disable();
            }

            // Up and down only act while the flight is on: the keys are independent, so the
            // creature never climbs because a chord was pressed by accident.
            if (s_flying)
            {
                float step = Mathf.Max(0.1f, DisguiseConfig.ConfigFlySpeed.Value) * Time.deltaTime;
                if (ChordHeld(s_upChord))
                {
                    s_height = Clamp(s_height + step);
                }
                if (ChordHeld(s_downChord))
                {
                    s_height = Clamp(s_height - step);
                }
            }

            Publish();
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Flight: {ex}");
        }
    }

    /// <summary>Starts the flight: the creature is raised by StartHeight right away.</summary>
    internal static void Enable()
    {
        if (s_flying)
        {
            return;
        }
        s_flying = true;
        s_height = StartHeight;
        s_nextPublishTime = 0f; // published by the next Tick, without waiting
        Disguise.LogInfo(
            $"Flight: on, +{StartHeight:0.0} m above you - your player stays on the ground."
        );
    }

    /// <summary>Ends the flight: the creature is back on the ground.</summary>
    internal static void Disable()
    {
        if (!s_flying && s_height <= 0f)
        {
            return;
        }
        s_flying = false;
        s_height = 0f;
        s_nextPublishTime = 0f;
        Disguise.LogInfo("Flight: off, the creature is back on the ground.");
    }

    /// <summary>True while <paramref name="chord"/> is held entirely (empty chord: never).</summary>
    private static bool ChordHeld(KeyCode[] chord)
    {
        for (int i = 0; i < chord.Length; i++)
        {
            if (!Input.GetKey(chord[i]))
            {
                return false;
            }
        }
        return chord.Length > 0;
    }

    /// <summary>
    /// True when every key of the chord is held and one of them went down this frame, so the
    /// chord works whatever order the keys were pressed in.
    /// </summary>
    private static bool ChordPressed(KeyCode[] chord)
    {
        if (chord.Length == 0)
        {
            return false;
        }
        bool pressed = false;
        for (int i = 0; i < chord.Length; i++)
        {
            if (!Input.GetKey(chord[i]))
            {
                return false;
            }
            if (Input.GetKeyDown(chord[i]))
            {
                pressed = true;
            }
        }
        return pressed;
    }

    /// <summary>
    /// "LeftAlt+UpArrow" - every key held at once. An unusable value disables that one
    /// binding and warns (the chords default to LeftAlt, never AltGr: on a French keyboard
    /// AltGr is what types characters while text is entered).
    /// </summary>
    private static KeyCode[] ParseChord(string setting, string text)
    {
        List<KeyCode> keys = new();
        foreach (string part in text.Split('+'))
        {
            if (!Enum.TryParse(part.Trim(), true, out KeyCode key))
            {
                keys.Clear();
                break;
            }
            keys.Add(key);
        }
        if (keys.Count == 0)
        {
            Disguise.LogWarning(
                $"Flight: \"{text}\" is not a usable key for {setting} "
                    + "(e.g. LeftAlt+UpArrow): that binding is disabled."
            );
            return Array.Empty<KeyCode>();
        }
        return keys.ToArray();
    }

    private static float Clamp(float height)
    {
        return Mathf.Clamp(height, 0f, MaxHeight);
    }

    /// <summary>Called by DisguiseConfig when one of the four key settings changes.</summary>
    internal static void OnSettingChanged()
    {
        s_chordsLoaded = false;
    }

    // ------------------------------------------------------------------ publication

    /// <summary>
    /// Writes the height into the player ZDO (Disguise.FlyKey, in CENTIMETRES: ZDO.Set has no
    /// float overload in the original assembly, and an int costs no extra member). The value
    /// is only written when it really changed, and at most every PublishInterval: the
    /// DataRevision is bumped by that write, so a key held down must not publish every frame.
    /// </summary>
    private static void Publish()
    {
        if (Time.unscaledTime < s_nextPublishTime)
        {
            return;
        }
        // The throttle also covers the "nothing to do" case: at rest this reads the local
        // ZDO ten times a second instead of once per frame.
        s_nextPublishTime = Time.unscaledTime + PublishInterval;

        ZDO? zdo = GameAccess.GetLocalPlayerZDO();
        if (zdo == null || !zdo.IsValid())
        {
            return;
        }
        int centimetres = Mathf.RoundToInt(s_height * 100f);
        if (zdo.GetInt(Disguise.FlyKey, 0) == centimetres)
        {
            return;
        }
        zdo.Set(Disguise.FlyKey, centimetres);
    }

    // ------------------------------------------------------- outgoing packet window

    /// <summary>
    /// Opens the window for one ZDOMan.SendZDOs call: every position that call asks
    /// ZDO.GetPosition for may then be raised (Patch_ZDOGetPosition). Only the relaying
    /// machine opens it - a client sending a raised position would have the server store it.
    /// </summary>
    internal static void BeginSend()
    {
        s_sending = ZNet.instance != null && ZNet.instance.IsServer();
    }

    /// <summary>Closes the window: called by the Postfix and by the Finalizer.</summary>
    internal static void EndSend()
    {
        s_sending = false;
    }

    /// <summary>
    /// Server, outgoing packet only: adds the height the owner published (cd_fly, in
    /// centimetres) to the position that goes into the packet header, so every machine that
    /// only renders this player - a vanilla client above all - draws the creature in the
    /// air. GetPosition hands out a copy: nothing is written into the ZDO here, hence there
    /// is nothing to put back.
    /// </summary>
    internal static void RaiseSentPosition(ZDO zdo, ref Vector3 position)
    {
        if (!s_sending)
        {
            return;
        }
        try
        {
            if (zdo.GetPrefab() == PrefabResolver.PlayerPrefabHash)
            {
                return; // not disguised: never raise a viking
            }
            int centimetres = zdo.GetInt(Disguise.FlyKey, 0);
            if (centimetres <= 0)
            {
                return;
            }
            position.y += centimetres / 100f;
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Flight raise (outgoing position): {ex}");
        }
    }
}
