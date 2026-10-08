#nullable enable
using System;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Where the costume stands and which way it faces: the two vectors the window edits and the
/// console sets - an OFFSET in world units (Valheim counts one of them as a metre, so there
/// is no conversion anywhere) and a ROTATION in degrees - what your own screen draws with
/// them, and the two places the other machines get them.
///
/// Your player NEVER moves. The real ZDO position - what the server stores in the save, runs
/// the AI and the combat with - stays where you stand, and the rotation stored with it stays
/// the one the game gave you; only what the other machines DRAW is moved and turned:
///  - your own screen: SelfView adds ViewOffset to the creature clone and composes
///    ViewRotation into the rotation that clone is given (SeeYourDisguise);
///  - every other machine: while the relaying machine builds an outgoing packet, the offset
///    published in cd_ox / cd_oy / cd_oz is added to the position that packet carries for
///    that ZDO (Patch_ZDOGetPosition), and the rotation published in cd_rx / cd_ry / cd_rz is
///    composed into m_rotation for the duration of that one body (Patch_ZDOSerialize).
///
/// Both windows open ONLY when this machine IS the server - a client moving its own packet
/// would have the server store it - so others see the pose only when this DLL runs on the
/// server, which is the same condition as the flight (FlightControl) and the ground rest
/// (RestOffset). Nothing beyond those six keys is ever written into the ZDO: the moved
/// position is handed back as a copy, and the rotation is put back the moment the body has
/// been built, so saves and game logic keep the grounded, un-turned player.
///
/// The pose is memory only, like the height of the flight: it starts at zero on every launch
/// and the publications below put that zero back into the ZDO, which is what erases a value
/// an earlier session may have left there.
/// </summary>
internal static class PoseControl
{
    /// <summary>Farthest the costume can be moved away from its player (world units).</summary>
    private const float MaxOffset = 1000f;

    /// <summary>
    /// Farthest turn in degrees - ten full turns, far past anything worth looking at. Kept
    /// finite so that the published value (a hundredth of a degree in an int) cannot overflow.
    /// </summary>
    private const float MaxTurn = 3600f;

    /// <summary>Smallest gap between two publications of the pose (seconds).</summary>
    private const float PublishInterval = 0.1f;

    /// <summary>
    /// How much a published unit is split into: the keys are ints (ZDO.Set has no float
    /// overload in the original assembly), so an offset goes out in CENTIMETRES and a rotation
    /// in CENTIDEGREES - the same packing the flight height uses, and invisible everywhere the
    /// pose is read (it is only ever multiplied back out of the keys).
    /// </summary>
    private const float Precision = 100f;

    private static Vector3 s_offset;
    private static Vector3 s_euler;
    private static float s_nextPublishTime;

    // Outgoing-packet window (server only): true exactly while ZDOMan.SendZDOs is building
    // the packet, the only place a position leaves this machine. Patch_SendZDOs opens it.
    private static bool s_sending;

    // Rotation window (server only): the ZDO whose stored rotation EndOutgoingRotation puts
    // back, and the value to put back. Patch_ZDOSerialize opens and closes it.
    private static ZDO? s_activeZdo;
    private static Vector3 s_savedRotation;

    /// <summary>What the local creature clone adds to the player position, every frame.</summary>
    internal static Vector3 ViewOffset => s_offset;

    /// <summary>
    /// The turn the local clone is given on top of the player's own rotation. Composed AFTER
    /// it (base * turn), which is exactly what the relaying machine writes into the packet, so
    /// the two views agree: a turn of zero changes nothing at all.
    /// </summary>
    internal static Quaternion ViewRotation => Quaternion.Euler(s_euler);

    /// <summary>Where the costume stands relative to its player (world units).</summary>
    internal static Vector3 Offset => s_offset;

    /// <summary>How far the costume is turned, in degrees around its own axes.</summary>
    internal static Vector3 Euler => s_euler;

    // ------------------------------------------------------------------ keys and state

    /// <summary>
    /// Moves the costume. Clamped, remembered, and published by the next Tick: zeroing
    /// s_nextPublishTime is what makes that happen on the next frame instead of in ten
    /// seconds, and several changes inside one frame still cost a single write.
    /// </summary>
    internal static void SetOffset(float x, float y, float z)
    {
        s_offset = new Vector3(ClampOffset(x), ClampOffset(y), ClampOffset(z));
        s_nextPublishTime = 0f;
    }

    /// <summary>Turns the costume in place - zero is the rotation the game already gave it.</summary>
    internal static void SetRotation(float x, float y, float z)
    {
        s_euler = new Vector3(ClampTurn(x), ClampTurn(y), ClampTurn(z));
        s_nextPublishTime = 0f;
    }

    /// <summary>Publishes the pose into the player ZDO: Disguise.Update, once per frame.</summary>
    internal static void Tick()
    {
        try
        {
            Publish();
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Pose publish: {ex}");
        }
    }

    /// <summary>
    /// Writes the six values into the player ZDO (Precision: ZDO.Set has no float overload in
    /// the original assembly, and an int costs no extra member). Only a value that really
    /// changed is written, and at most every PublishInterval: every write bumps the
    /// DataRevision and re-sends the whole ZDO, so typing a number must not publish on every
    /// keystroke. Reading all six at rest therefore costs six ints ten times a second, and
    /// nothing at all once they agree with the ZDO.
    /// </summary>
    private static void Publish()
    {
        if (Time.unscaledTime < s_nextPublishTime)
        {
            return;
        }
        s_nextPublishTime = Time.unscaledTime + PublishInterval;

        ZDO? zdo = GameAccess.GetLocalPlayerZDO();
        if (zdo == null || !zdo.IsValid())
        {
            return;
        }

        SetIfChanged(zdo, Disguise.OffsetXKey, Mathf.RoundToInt(s_offset.x * Precision));
        SetIfChanged(zdo, Disguise.OffsetYKey, Mathf.RoundToInt(s_offset.y * Precision));
        SetIfChanged(zdo, Disguise.OffsetZKey, Mathf.RoundToInt(s_offset.z * Precision));
        SetIfChanged(zdo, Disguise.RotationXKey, Mathf.RoundToInt(s_euler.x * Precision));
        SetIfChanged(zdo, Disguise.RotationYKey, Mathf.RoundToInt(s_euler.y * Precision));
        SetIfChanged(zdo, Disguise.RotationZKey, Mathf.RoundToInt(s_euler.z * Precision));
    }

    private static void SetIfChanged(ZDO zdo, string key, int value)
    {
        if (zdo.GetInt(key, 0) == value)
        {
            return;
        }
        zdo.Set(key, value);
    }

    // ------------------------------------------------------- outgoing packet window

    /// <summary>
    /// Opens the window for one ZDOMan.SendZDOs call: every position that call asks
    /// ZDO.GetPosition for may then be moved (Patch_ZDOGetPosition). Only the relaying
    /// machine opens it - a client sending a moved position would have the server store it.
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
    /// Server, outgoing packet only: adds the offset the owner published (cd_ox / cd_oy /
    /// cd_oz, in centimetres) to the position that goes into the packet header, so every
    /// machine that only renders this player - a vanilla client above all - draws the costume
    /// where the owner asked for it. GetPosition hands out a copy: nothing is written into the
    /// ZDO here, hence there is nothing to put back.
    ///
    /// It runs AFTER the ground rest (Patch_ZDOGetPosition), so the costume keeps exactly the
    /// lift RestOffset found under its player and the offset is added on top of it - the same
    /// order the flight uses, and the reason the two views agree. The observer refresh is left
    /// alone: the far-away position it fakes has to arrive as faked.
    /// </summary>
    internal static void RaiseSentPosition(ZDO zdo, ref Vector3 position)
    {
        if (!s_sending)
        {
            return;
        }
        try
        {
            if (
                zdo.GetPrefab() == PrefabResolver.PlayerPrefabHash
                || ObserverRefresh.s_refreshZdo == zdo
            )
            {
                return; // not disguised: never move a viking, never move a faked position
            }
            int x = zdo.GetInt(Disguise.OffsetXKey, 0);
            int y = zdo.GetInt(Disguise.OffsetYKey, 0);
            int z = zdo.GetInt(Disguise.OffsetZKey, 0);
            if (x == 0 && y == 0 && z == 0)
            {
                return;
            }
            position.x += x / Precision;
            position.y += y / Precision;
            position.z += z / Precision;
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Pose raise (outgoing position): {ex}");
        }
    }

    // ------------------------------------------------------------- rotation window

    /// <summary>
    /// ZDO.Serialize: composes the turn the owner published (cd_rx / cd_ry / cd_rz, in
    /// centidegrees) into the rotation of the outgoing body for that one call, and leaves
    /// nothing behind. Only the relaying machine ever swaps - an owner rewriting its own
    /// rotation would have the server STORE it, which is the rule RestOffset follows for the
    /// same reason - only for a disguised player, and only when there is a turn to apply.
    /// The stored rotation, the save file and your own view are therefore never touched.
    ///
    /// The body writes m_rotation only when it differs from identity, and RestOffset composes
    /// its lay-on-the-ground tilt into the same value: Patch_ZDOSerialize opens this one
    /// first and closes it last, so a costume that is both turned and laid on a slope leaves
    /// the server as one single rotation, in which the tilt is applied on top of the turn.
    ///
    /// Returns true when a swap really happened: the caller must then call
    /// EndOutgoingRotation, which is harmless when nothing was opened.
    /// </summary>
    internal static bool BeginOutgoingRotation(ZDO? zdo)
    {
        if (
            s_activeZdo != null
            || zdo == null
            || !zdo.IsValid()
            || GameAccess.s_zdoRotationField == null
        )
        {
            return false;
        }
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            return false;
        }
        if (zdo.GetPrefab() == PrefabResolver.PlayerPrefabHash)
        {
            return false; // not disguised: never turn a viking
        }
        int x = zdo.GetInt(Disguise.RotationXKey, 0);
        int y = zdo.GetInt(Disguise.RotationYKey, 0);
        int z = zdo.GetInt(Disguise.RotationZKey, 0);
        if (x == 0 && y == 0 && z == 0)
        {
            return false; // the rotation the game has is the one that goes out
        }

        Vector3 saved = (Vector3)GameAccess.s_zdoRotationField.GetValue(zdo);
        Quaternion current = Quaternion.Euler(saved);
        Quaternion turn = Quaternion.Euler(
            new Vector3(x / Precision, y / Precision, z / Precision)
        );
        s_activeZdo = zdo;
        s_savedRotation = saved;
        GameAccess.s_zdoRotationField.SetValue(zdo, (current * turn).eulerAngles);
        return true;
    }

    /// <summary>Puts the rotation back: called by the Finalizer even when no swap happened.</summary>
    internal static void EndOutgoingRotation()
    {
        ZDO? zdo = s_activeZdo;
        Vector3 saved = s_savedRotation;
        s_activeZdo = null;
        if (zdo == null || GameAccess.s_zdoRotationField == null)
        {
            return;
        }
        try
        {
            GameAccess.s_zdoRotationField.SetValue(zdo, saved);
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Pose rotation (restore): {ex}");
        }
    }

    // ------------------------------------------------------------------ limits

    private static float ClampOffset(float value)
    {
        return Mathf.Clamp(value, -MaxOffset, MaxOffset);
    }

    private static float ClampTurn(float value)
    {
        return Mathf.Clamp(value, -MaxTurn, MaxTurn);
    }
}
