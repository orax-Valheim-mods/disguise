#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Server, outgoing packet only: hands back the position of a disguised player raised to
/// where its costume actually RESTS, and lays that costume on the same ground while the
/// outgoing body is built - which is what stops it shivering on a machine running no DLL of
/// ours.
///
/// Why the shiver survives the band that freezes the packet (PositionHold): on the receiving
/// side ZSyncTransform turns gravity and velocity off, puts the body to sleep, and then -
/// every frame - drags it back to the relayed position with a 0.2 lerp as soon as it has
/// moved more than a millimetre. Whatever pushes the costume out of the ground therefore
/// comes straight back, and the two together make a cycle of a few centimetres. The costume
/// cannot make room by itself either: it wears YOUR upright rotation, so on a slope its
/// uphill edge is buried however carefully its height was set, and the cart's wheels are
/// separate bodies on joints the ground keeps lifting against the root. Nothing else moves a
/// body configured that way - which is exactly what your tests showed: no shiver in water,
/// none in the air, none on your own clone (GhostFactory strips its rigidbody and colliders,
/// so it has no physics to lose the fight with).
///
/// Both levers are measured once per prefab, from the geometry that can actually collide:
/// COLLIDERS only, because only the solver's shapes are pushed back against - a mesh whose
/// bounds hang far below its own hull can ask for a lift no contact ever needed (both
/// numbers are logged side by side, so the difference is there to read).
///
/// The ground under them is read here, and never guessed: the game's own answer is a
/// physics ray that replies with the very height it was handed when it hits NOTHING, so a
/// missing collider and a perfectly flat slope come out as one number - which is how a
/// costume ends up lying flat on a hillside while its log claims the ground was level. The
/// ray is therefore cast again by hand (hit and miss both known), the ground is probed for
/// the component that makes it terrain whatever layer it answers to, and the loaded
/// heightmap answers when no collider did. Whichever of the three answered - or that none
/// could - is printed by the report along with the height it read against the player's feet,
/// and a read that answers with nothing prints its own self-test: the position, the layer
/// and its mask, what the unfiltered ray meets on the way down, and how many terrain maps
/// this machine has registered.
///
/// - The HEIGHT: everything the costume hangs below the level its player stands on, whatever
///   part of the ground under its footprint the fitted plane cannot explain, and a few
///   centimetres of clearance. That clearance is what carries the fix when the measurement is
///   a little off: a costume that touches nothing has nothing to be pushed out of.
/// - The ROTATION: laid, for the duration of the outgoing body, on the plane fitted under
///   that footprint - but only for a costume whose ZSyncTransform really applies rotations
///   (s_upright), since a tilt the rendering machine ignores must not be paid for in height.
///   A raft then lies ON a slope like a real one, and what is left of the rise afterwards is
///   only what the plane could not explain.
///
/// Like the flight raise, this hands back a copy: the owner's zdo, the server's own state,
/// the saves and the game logic always keep the player on the ground. Patch_SendZDOs opens
/// the window around one ZDOMan.SendZDOs call, and the same two values are rebuilt on your
/// own screen (SelfView) so that both views of a costume agree to the centimetre.
/// </summary>
internal static class RestOffset
{
    /// <summary>Ray samples around the costume: the rise and the slope of the ground under its footprint.</summary>
    private const int RingSamples = 8;

    /// <summary>
    /// How far the terrain may be from the player's own feet for the two to be treated as
    /// resting on the same ground. Well below: a floor over it, water, air. Well above: the
    /// ray found a roof, and its height says nothing about what is under the feet.
    /// </summary>
    private const float GroundBand = 0.5f;

    /// <summary>
    /// Largest ground rise the footprint may buy on top of the plane: half a metre of it is
    /// already a costume hanging over a cliff edge, and past that the float would be worse
    /// than the shiver it replaces. On an ordinary slope the plane absorbs the rise, so what
    /// is left for this to cover is only its curvature.
    /// </summary>
    private const float MaxFootprintRise = 0.5f;

    /// <summary>
    /// Steepest ground a costume is laid on. Past 45 degrees the fitted plane stops
    /// describing a slope a costume can stand on and starts describing a cliff; the tilt and
    /// the rise are held to it together so that the height never assumes more than the
    /// rotation will deliver.
    /// </summary>
    private const float MaxSlope = 1f;

    /// <summary>A reach under this is a round bottom: the ground there is the ground at the centre.</summary>
    private const float MinReach = 0.05f;

    /// <summary>
    /// Centimetres kept between a costume and the ground it is relayed over: the difference
    /// between resting exactly on the surface - which the solver keeps resolving - and
    /// touching nothing at all, and what absorbs the error of a measurement taken on a
    /// prefab asset while the real body is somewhere else.
    /// </summary>
    private const float Clearance = 0.05f;

    /// <summary>
    /// Nothing may be lifted by more than this. A costume really does hang a metre below its
    /// pivot (a hull, a set of wheels), so this is not a guess at how deep one should sit: it
    /// is the alarm against a measurement gone wrong, and the log says when it fires.
    /// </summary>
    private const float MaxLift = 2.5f;

    /// <summary>
    /// Where the ground ray starts, and how far it reaches: the same numbers, and the same
    /// terrain layer, as the game's own ground read - the one difference being that this
    /// ray reports a miss instead of answering with the height it was given.
    /// </summary>
    private const float GroundRayTop = 6000f;

    private const float GroundRayLength = 10000f;

    /// <summary>The only layer read as ground: the terrain itself, never a piece.</summary>
    private static readonly int s_terrainMask = LayerMask.GetMask("terrain");

    /// <summary>
    /// Which of the four reads answered: the terrain-layer ray, the probe that recognises
    /// the ground by the component it carries, the loaded heightmap, or - on a machine with
    /// no terrain instance where the costume stands - the zone built for this read. The
    /// report prints it so that a machine whose layer answers to nothing cannot pass for
    /// flat ground.
    /// </summary>
    private const int RaySource = 0;

    private const int ProbeSource = 1;

    private const int HeightmapSource = 2;

    private const int ZoneSource = 3;

    /// <summary>
    /// The probe's ray results: preallocated, because it runs for every sample of every ring
    /// on a machine where the layer ray never hits - and reading the ground may not cost one
    /// allocation per sample. Triggers are never terrain, so they are not asked for.
    /// </summary>
    private const int ProbeCapacity = 48;

    private static readonly RaycastHit[] s_probeHits = new RaycastHit[ProbeCapacity];

    /// <summary>
    /// Measured once per costume, by prefab hash: x = how far its lowest COLLIDER hangs below
    /// the pivot (never above it); y = how far its flat bottom reaches sideways.
    /// </summary>
    private static readonly Dictionary<int, Vector2> s_footprints = new Dictionary<int, Vector2>();

    /// <summary>
    /// Costumes measured and deliberately left alone - whatever else is true of them, the
    /// machines rendering them are not in a fight with the ground (see FightsTheGround).
    /// </summary>
    private static readonly HashSet<int> s_leftAlone = new HashSet<int>();

    /// <summary>
    /// Costumes whose ZSyncTransform carries m_syncRotation = false. Laying one on the ground
    /// would change nothing where it is rendered, while its height would still be spent
    /// following a plane it never turns onto: they keep the height rule alone, which is also
    /// the safe direction for a component we failed to read.
    /// </summary>
    private static readonly HashSet<int> s_upright = new HashSet<int>();

    /// <summary>True while the positions this call asks for are landing in a packet.</summary>
    private static bool s_sending;

    /// <summary>
    /// The ZDO whose ground is about to be written into its rotation, and that ground's
    /// normal. Raised by Raise, which runs on the very ZDO a few lines later handed to
    /// ZDO.Serialize, and consumed there: BeginOutgoingRotation must find exactly this ZDO or
    /// it touches nothing at all.
    /// </summary>
    private static ZDO? s_pendingZdo;

    private static Vector3 s_pendingNormal = Vector3.up;

    /// <summary>
    /// Rotation swap in progress: the raw euler angles saved for the duration of one
    /// ZDO.Serialize, put back by EndRotation whatever becomes of that call.
    /// </summary>
    private static ZDO? s_activeZdo;

    private static Vector3 s_savedRotation;

    /// <summary>Ring of the last ground fit, kept here so that Rest never allocates.</summary>
    private static readonly float[] s_sampleX = new float[RingSamples];

    private static readonly float[] s_sampleZ = new float[RingSamples];

    private static readonly float[] s_sampleH = new float[RingSamples];

    /// <summary>Own view: the lift and the normal recomputed at most ten times a second (it costs rays).</summary>
    private static bool s_hasView;

    private static int s_viewHash;

    private static Vector3 s_viewPosition;

    private static float s_viewNext;

    private static float s_viewLift;

    private static Vector3 s_viewNormal = Vector3.up;

    /// <summary>
    /// Ground logging: the last line reported, and the earliest next one. The line is
    /// compared as a WHOLE, because a tilt moves the lift by less than the two centimetres
    /// this used to compare on - reporting on the height alone would never mention a slope,
    /// and a costume lying flat on one looks exactly like one standing on level ground.
    /// </summary>
    private static string? s_lastReport;

    private static float s_nextReport;

    /// <summary>
    /// The earliest next self-test line about a ground no read could answer: once when it
    /// first happens, then at most every half minute, so a failure nobody can see from the
    /// report alone still costs one line rather than one per packet.
    /// </summary>
    private static float s_nextNoGround;

    /// <summary>
    /// Rotation logging: the angle last written into an outgoing body (NaN until the first
    /// one), and the earliest next line about the swap in either direction. Computed and
    /// sent are two different answers; only this one reaches the machine that shivers.
    /// </summary>
    private static float s_writtenDegrees = float.NaN;

    private static float s_nextSwapLog;

    /// <summary>
    /// Opens the window for one ZDOMan.SendZDOs call. Only the relaying machine opens it: a
    /// client sending a raised position would have the server STORE it.
    /// </summary>
    internal static void BeginSend()
    {
        s_sending = ZNet.instance != null && ZNet.instance.IsServer();
        s_pendingZdo = null;
        s_pendingNormal = Vector3.up;
    }

    /// <summary>Closes the window: called by the Postfix and by the Finalizer.</summary>
    internal static void EndSend()
    {
        s_sending = false;
        s_pendingZdo = null;
        s_pendingNormal = Vector3.up;
    }

    /// <summary>
    /// Raises the copy ZDO.GetPosition is about to hand to the packet builder by what its
    /// costume needs to stop digging in, and remembers the ground under that very ZDO for the
    /// rotation of the body built from it right after. No-op outside the window, for a
    /// viking, for anything without the marker of a disguised player (a world object is
    /// relayed exactly as the game has it), and while the observer refresh fakes its
    /// far-away position - that one has to arrive as faked.
    /// </summary>
    internal static void Raise(ZDO zdo, ref Vector3 position)
    {
        // Whichever ZDO asked last: a rotation is only ever rewritten for the one whose
        // ground is in here right now, so forget the previous answer before anything else.
        s_pendingZdo = null;
        s_pendingNormal = Vector3.up;
        if (!s_sending)
        {
            return;
        }
        try
        {
            int hash = zdo.GetPrefab();
            if (hash == PrefabResolver.PlayerPrefabHash || ObserverRefresh.s_refreshZdo == zdo)
            {
                return;
            }
            if (!DisguiseApplier.HasMarker(zdo))
            {
                return; // not one of ours: hold nothing, lift nothing, tilt nothing
            }
            Rest(
                hash,
                position,
                out float lift,
                out Vector3 normal,
                out float rise,
                out float groundDelta,
                out int groundSource
            );
            s_pendingZdo = zdo;
            s_pendingNormal = normal;
            if (lift > 0f)
            {
                // A costume lifted from its hang alone while nothing at all could be read
                // under it is the one case the report cannot explain on its own: name the
                // cause here, where the ground was actually asked for.
                if (float.IsNaN(groundDelta))
                {
                    ReportNoGround(hash, position);
                }
                Report(hash, lift, normal, rise, groundDelta, groundSource);
                position.y += lift;
            }
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Ground rest (outgoing position): {ex}");
        }
    }

    /// <summary>
    /// ZDO.Serialize: lays the costume on the ground Raise measured for it, for the duration
    /// of the outgoing body only, and leaves nothing behind. Only the relaying machine ever
    /// swaps (an owner rewriting its own rotation would have the server STORE it), and only
    /// for the exact ZDO that Raise measured - so a Serialize outside the window, or one for
    /// anything else, changes nothing. Returns true when a swap really happened: the caller
    /// must then call EndRotation, which is harmless when nothing was opened.
    ///
    /// The body decides whether to write a rotation AT ALL from the value it finds, and that
    /// code runs after this prefix: a costume whose tilt makes its euler angles non-zero has
    /// its rotation written, while one resting on flat ground is handed over exactly as the
    /// game has it.
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
        ZDO? measured = s_pendingZdo;
        if (measured != zdo)
        {
            // A ground measured and then never laid down: the body being built right now
            // belongs to another ZDO than the one Raise lifted, so the tilt is dropped with
            // nothing to show for it. Rare enough to be worth a line - it is the failure a
            // flat report cannot tell apart from real flat ground.
            if (
                measured != null
                && DisguiseConfig.ConfigLogs.Value
                && Time.realtimeSinceStartup >= s_nextSwapLog
            )
            {
                s_nextSwapLog = Time.realtimeSinceStartup + 1f;
                Disguise.LogInfo(
                    $"Ground rest: tilt measured for {PrefabResolver.PrefabName(measured.GetPrefab())} "
                        + "was dropped: ZDO.Serialize ran for another ZDO."
                );
            }
            return false;
        }
        // Consumed: the same body must never be laid on the ground twice.
        s_pendingZdo = null;
        if (s_pendingNormal == Vector3.up)
        {
            return false; // flat ground: there is nothing to lay down onto
        }
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            return false;
        }
        Vector3 saved = (Vector3)GameAccess.s_zdoRotationField.GetValue(zdo);
        Quaternion current = Quaternion.Euler(saved);
        Quaternion tilt = Quaternion.FromToRotation(current * Vector3.up, s_pendingNormal);
        if (tilt == Quaternion.identity)
        {
            return false;
        }
        s_activeZdo = zdo;
        s_savedRotation = saved;
        GameAccess.s_zdoRotationField.SetValue(zdo, (tilt * current).eulerAngles);
        ReportWritten(zdo, Vector3.Angle(current * Vector3.up, s_pendingNormal));
        return true;
    }

    /// <summary>Puts the rotation back: called by the Finalizer even when the swap never happened.</summary>
    internal static void EndRotation()
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
            Disguise.LogError($"Ground rest (rotation restore): {ex}");
        }
    }

    /// <summary>
    /// The same lift for the clone your own screen draws, so that the two views of a costume
    /// agree. Recomputed at most ten times a second, and only after the costume has moved
    /// more than twenty centimetres: it is a display detail, the packet gets the exact one.
    /// </summary>
    internal static Vector3 ViewOffset(int prefabHash, Vector3 position)
    {
        try
        {
            RefreshView(prefabHash, position);
            return Vector3.up * s_viewLift;
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Ground rest (own view): {ex}");
            return Vector3.zero;
        }
    }

    /// <summary>
    /// The same lay-on-the-ground rotation the packet carries, rebuilt for the clone your own
    /// screen draws from the normal and the rotation it is about to be given. Nothing is
    /// transmitted between the two machines: they measure the same ground over the same
    /// footprint and reach the same pose.
    ///
    /// The caller composes the pose turn ON TOP of this result, exactly as Patch_ZDOSerialize
    /// composes it on top of the same tilt in the packet - and it has to be on top: this is a
    /// FromToRotation onto the ground normal, so it pins the up axis of whatever it lands on
    /// and would take a turn around X or Z straight back out, leaving only the yaw.
    /// </summary>
    internal static Quaternion ViewRotation(int prefabHash, Vector3 position, Quaternion rotation)
    {
        try
        {
            RefreshView(prefabHash, position);
            return Quaternion.FromToRotation(rotation * Vector3.up, s_viewNormal) * rotation;
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Ground rest (own view rotation): {ex}");
            return rotation;
        }
    }

    // ------------------------------------------------------------------ internals

    private static void RefreshView(int prefabHash, Vector3 position)
    {
        if (
            s_hasView
            && prefabHash == s_viewHash
            && Time.unscaledTime < s_viewNext
            && (position - s_viewPosition).sqrMagnitude < 0.04f
        )
        {
            return;
        }
        s_hasView = true;
        s_viewHash = prefabHash;
        s_viewPosition = position;
        s_viewNext = Time.unscaledTime + 0.1f;
        Rest(prefabHash, position, out s_viewLift, out s_viewNormal, out _, out _, out _);
    }

    /// <summary>
    /// How much higher than its player this costume must be relayed, the normal of the
    /// ground it should be laid on, and the two numbers behind them: the part of the ground
    /// the fitted plane could not explain, and the height read under the player's own feet
    /// with the source it came from (NaN when nothing at all could be read).
    ///
    /// The TILT needs the terrain to be what the costume rests on - a floor, water or thin
    /// air lie far below, a roof far above, and neither has a slope worth following. The
    /// RISE does not ask that much: it can only raise a costume, never lower it, and only
    /// over ground rising above the feet, which is exactly what buries a costume on a
    /// hillside while its player stands a plank higher than the terrain. The clearance is
    /// spent either way, since a costume on a deck rests on something the ray never finds.
    /// </summary>
    private static void Rest(
        int prefabHash,
        Vector3 position,
        out float lift,
        out Vector3 normal,
        out float rise,
        out float groundDelta,
        out int groundSource
    )
    {
        normal = Vector3.up;
        lift = 0f;
        rise = 0f;
        groundDelta = float.NaN;
        groundSource = RaySource;
        if (!Footprint(prefabHash, out Vector2 footprint))
        {
            return;
        }

        float best = position.y;
        if (footprint.y > MinReach && TryGround(position, out float ground, out groundSource))
        {
            // Kept even when it turns the fit down: a costume relayed flat on a slope must
            // be traceable to this test rather than guessed at.
            groundDelta = ground - position.y;
            if (groundDelta <= GroundBand)
            {
                bool tilt = Mathf.Abs(groundDelta) <= GroundBand && !s_upright.Contains(prefabHash);
                best = Fit(position, footprint.y, ground, !tilt, out normal);
            }
        }

        // What the fitted plane, laid across the costume, still has to be lifted over: with
        // the tilt applied the slope is already carried by the plane, and only what it cannot
        // explain is left for the height. Outside the band there is nothing to lift over at
        // all, and the answer is the hang plus the clearance.
        float above = best - position.y;
        if (above < 0f)
        {
            above = 0f;
        }
        else if (above > MaxFootprintRise)
        {
            above = MaxFootprintRise;
        }
        rise = above;

        // The costume's lowest collider, measured along its own tilted up: laying a costume
        // down lowers the point that used to hang straight under the pivot.
        float hang = Mathf.Max(0f, -footprint.x * normal.y);
        lift = (hang > MaxLift ? MaxLift : hang) + above + Clearance;
    }

    /// <summary>
    /// The terrain surface under a point, read four ways before nothing is admitted: the
    /// terrain-layer ray first (exact, and it follows whatever the terrain was sculpted
    /// into), then the probe that recognises the ground by the component it carries - for a
    /// machine where that layer answers to nothing - then the loaded heightmap, which needs
    /// no collider at all, and last the zone built for this read when the machine has no
    /// terrain instance where the costume stands (a dedicated server, which generates those
    /// zones without ever leaving one standing). False only when all four have nothing to
    /// say, a case the report prints rather than letting it pass for flat ground, with a
    /// self-test line naming which of them failed and why.
    ///
    /// Every read touches the zone built for its point first: from the moment that zone
    /// exists the ray above answers from its collider, so the build is the FIRST read of a
    /// zone and the ones after it cost one dictionary lookup.
    /// </summary>
    private static bool TryGround(Vector3 position, out float height, out int source)
    {
        GroundProbe.Touch(position);
        if (TryLayerRay(position, out height))
        {
            source = RaySource;
            return true;
        }
        if (TryProbe(position, out height))
        {
            source = ProbeSource;
            return true;
        }
        if (GameAccess.TryGetTerrainHeight(position, out height))
        {
            source = HeightmapSource;
            return true;
        }
        // Only the relaying machine builds: a client reads the terrain it already has, and
        // building one from a view - which may be asked at a faked position - would plant a
        // zone where nobody ever stood.
        if (s_sending && GroundProbe.TryGetHeight(position, out height))
        {
            source = ZoneSource;
            return true;
        }
        source = RaySource;
        height = 0f;
        return false;
    }

    /// <summary>The game's own read, cast again by hand so that a miss is a miss.</summary>
    private static bool TryLayerRay(Vector3 position, out float height)
    {
        Vector3 origin = position;
        origin.y = GroundRayTop;
        if (
            Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                GroundRayLength,
                s_terrainMask
            )
        )
        {
            height = hit.point.y;
            return true;
        }
        height = 0f;
        return false;
    }

    /// <summary>
    /// The same ray through every layer, keeping only what carries a Heightmap - how the
    /// game itself reads a collider back to learn that it is terrain (GetGroundInfo). A
    /// floor, water, a roof and the costume's own hull are all skipped, so this answers with
    /// exactly the surface the layer ray was meant to find, whatever that layer is called on
    /// this machine; the topmost terrain wins, as it would have won the masked ray.
    /// </summary>
    private static bool TryProbe(Vector3 position, out float height)
    {
        height = 0f;
        Type? heightmap = GameAccess.s_heightmapType;
        if (heightmap == null)
        {
            return false;
        }
        Vector3 origin = position;
        origin.y = GroundRayTop;
        int count = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            s_probeHits,
            GroundRayLength,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore
        );
        bool found = false;
        float closest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = s_probeHits[i];
            if (hit.distance >= closest)
            {
                continue;
            }
            Collider? collider = hit.collider;
            if (collider == null || collider.GetComponent(heightmap) == null)
            {
                continue;
            }
            closest = hit.distance;
            height = hit.point.y;
            found = true;
        }
        return found;
    }

    /// <summary>
    /// The self-test: what no read of the ground could answer, once when it first fails and
    /// then at most every half minute. It names the four things the report cannot tell
    /// apart - a layer answering to nothing, a machine where no collider answers at all, a
    /// position no world was generated under, and a zone that could not be built to read it
    /// (with the reason that build gave) - so the next step is read rather than guessed at.
    /// </summary>
    private static void ReportNoGround(int prefabHash, Vector3 position)
    {
        if (!DisguiseConfig.ConfigLogs.Value || Time.realtimeSinceStartup < s_nextNoGround)
        {
            return;
        }
        s_nextNoGround = Time.realtimeSinceStartup + 30f;
        int layer = LayerMask.NameToLayer("terrain");
        int lookup = GameAccess.s_heightmapGetHeight == null ? -1 : GameAccess.CountHeightmaps();
        Disguise.LogInfo(
            $"Ground rest: no ground under {PrefabResolver.PrefabName(prefabHash)} at "
                + $"({position.x:F1}, {position.y:F1}, {position.z:F1}) | terrain mask "
                + $"{s_terrainMask} (layer {layer}) | {DescribeProbe(position)} | heightmaps "
                + $"{lookup}, Heightmap.GetHeight "
                + (GameAccess.s_heightmapGetHeight == null ? "not resolved" : "resolved")
                + $" | zone probe {GroundProbe.LastFailure()}"
        );
    }

    /// <summary>
    /// What the unfiltered ray meets on its way down: the first collider's name and layer,
    /// so that a terrain on an unexpected layer - or nothing at all - is a name in the log
    /// instead of a hypothesis.
    /// </summary>
    private static string DescribeProbe(Vector3 position)
    {
        Vector3 origin = position;
        origin.y = GroundRayTop;
        int count = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            s_probeHits,
            GroundRayLength,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore
        );
        if (count == 0)
        {
            return "ray meets nothing in 10000 m";
        }
        if (count >= s_probeHits.Length)
        {
            return $"ray meets {count}+ colliders (buffer full)";
        }
        int top = 0;
        float closest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (s_probeHits[i].distance < closest)
            {
                closest = s_probeHits[i].distance;
                top = i;
            }
        }
        Collider? collider = s_probeHits[top].collider;
        if (collider == null)
        {
            return "ray meets an unnamed collider";
        }
        return $"ray meets \"{collider.name}\" [{LayerMask.LayerToName(collider.gameObject.layer)}] at {s_probeHits[top].point.y:F1} m";
    }

    /// <summary>
    /// Samples the ground around the costume, lays a plane over those samples and returns the
    /// highest value the plane reaches over them - what is left for the height once the
    /// costume lies on it. That plane is capped to MaxSlope BEFORE being returned, so the
    /// normal handed back and the number computed here always describe the same ground: a
    /// costume whose rotation will not follow a cliff must not pay for one in height either.
    /// A costume kept upright is simply measured with a flat plane (slope zero).
    /// </summary>
    private static float Fit(
        Vector3 position,
        float reach,
        float ground,
        bool upright,
        out Vector3 normal
    )
    {
        normal = Vector3.up;
        float sumXX = 0f;
        float sumZZ = 0f;
        float sumXH = 0f;
        float sumZH = 0f;
        for (int i = 0; i < RingSamples; i++)
        {
            float angle = i * (2f * Mathf.PI / RingSamples);
            float dx = Mathf.Cos(angle) * reach;
            float dz = Mathf.Sin(angle) * reach;
            Vector3 sample = position;
            sample.x += dx;
            sample.z += dz;
            // A ring point with nothing to read under it (a zone edge, a missing collider)
            // is answered with the centre's own ground: it must not invent a rise, nor a
            // slope to explain one.
            if (!TryGround(sample, out float height, out _))
            {
                height = ground;
            }
            s_sampleX[i] = dx;
            s_sampleZ[i] = dz;
            s_sampleH[i] = height;
            sumXX += dx * dx;
            sumZZ += dz * dz;
            sumXH += dx * (height - ground);
            sumZH += dz * (height - ground);
        }

        float slopeX = 0f;
        float slopeZ = 0f;
        if (!upright)
        {
            // The ring is evenly spaced about the centre, so the two directions solve
            // independently: one division each, no system to invert.
            slopeX = sumXX > 0f ? sumXH / sumXX : 0f;
            slopeZ = sumZZ > 0f ? sumZH / sumZZ : 0f;
            float slope = Mathf.Sqrt(slopeX * slopeX + slopeZ * slopeZ);
            if (slope > 0.0001f)
            {
                if (slope > MaxSlope)
                {
                    slopeX *= MaxSlope / slope;
                    slopeZ *= MaxSlope / slope;
                }
                normal = new Vector3(-slopeX, 1f, -slopeZ).normalized;
            }
            else
            {
                slopeX = 0f;
                slopeZ = 0f;
            }
        }

        float best = ground;
        for (int i = 0; i < RingSamples; i++)
        {
            float abovePlane = s_sampleH[i] - ((s_sampleX[i] * slopeX) + (s_sampleZ[i] * slopeZ));
            if (abovePlane > best)
            {
                best = abovePlane;
            }
        }
        return best;
    }

    /// <summary>
    /// The costume's measured geometry, or false when it is one of those that are never
    /// lifted (recorded, so the test runs once per prefab and not once per packet).
    /// </summary>
    private static bool Footprint(int prefabHash, out Vector2 footprint)
    {
        if (s_footprints.TryGetValue(prefabHash, out footprint))
        {
            return true;
        }
        if (s_leftAlone.Contains(prefabHash))
        {
            return false;
        }
        ZNetScene? zns = ZNetScene.instance;
        GameObject? prefab = zns != null ? zns.GetPrefab(prefabHash) : null;
        if (prefab == null || !FightsTheGround(prefab))
        {
            s_leftAlone.Add(prefabHash);
            return false;
        }
        // Recorded before the measure: a costume that will not be turned stays upright even
        // if the measurement itself fails below.
        bool upright = !SyncsRotation(prefab);
        if (upright)
        {
            s_upright.Add(prefabHash);
        }
        footprint = Measure(prefab, out float meshLowest);
        s_footprints[prefabHash] = footprint;
        string name = PrefabResolver.PrefabName(prefabHash);
        Disguise.LogInfo(
            $"Ground rest: {name} hangs {-footprint.x:F3} m below its colliders, "
                + $"{-meshLowest:F3} m below its meshes, flat bottom {footprint.y:F2} m wide, "
                + (upright ? "kept upright." : "laid on the ground.")
        );
        if (-footprint.x > MaxLift)
        {
            Disguise.LogWarning(
                $"Ground rest: {name} hangs more than {MaxLift:F1} m below its colliders - "
                    + $"the lift is capped at {MaxLift:F1} m, so it may still dig in."
            );
        }
        return true;
    }

    /// <summary>
    /// Whether the machines rendering this costume apply the rotation the packet carries:
    /// ZSyncTransform.m_syncRotation, read through reflection exactly as PrefabCatalog reads
    /// the component itself. False when it cannot be read, which is the safe direction - a
    /// costume held upright with the height rule alone looks as it always did.
    /// </summary>
    private static bool SyncsRotation(GameObject prefab)
    {
        Component? sync = prefab.GetComponent(PrefabCatalog.SyncTransformComponent);
        if (sync == null)
        {
            return false;
        }
        FieldInfo? field = sync.GetType().GetField("m_syncRotation");
        if (field == null || field.FieldType != typeof(bool))
        {
            return false;
        }
        return field.GetValue(sync) is bool value && value;
    }

    /// <summary>
    /// Whether the machine that only renders this costume can end up fighting the ground -
    /// and only those get lifted: ZSyncTransform (without it nothing pulls the object back to
    /// the zdo, so there is no cycle to break), a rigidbody that is not kinematic (a kinematic
    /// one is moved with MovePosition, which no contact can push back), and no Character of
    /// its own (a creature settles itself through its own script - the reason costume
    /// creatures never shivered). The component is read by NAME, as PrefabCatalog does.
    /// </summary>
    private static bool FightsTheGround(GameObject prefab)
    {
        if (prefab.GetComponent(PrefabCatalog.SyncTransformComponent) == null)
        {
            return false;
        }
        Rigidbody? body = prefab.GetComponent<Rigidbody>();
        if (body == null || body.isKinematic)
        {
            return false;
        }
        return prefab.GetComponent<Character>() == null;
    }

    /// <summary>
    /// The costume's own geometry, measured on the prefab asset in its own frame: how far
    /// below its pivot its COLLIDERS hang, and how far sideways its FLAT bottom reaches - the
    /// part that can be inside a slope. A sphere or a capsule is not counted as flat: it
    /// touches the ground at one point, and the ground there is the ground at the centre.
    ///
    /// Meshes are measured as well but never used for the lift, only for the log line: a mesh
    /// is not something the solver pushes a costume out of, and its bounds are not always the
    /// hull they belong to. The two numbers side by side are what tells a costume that really
    /// does sit a metre deep from one whose art merely reaches that far down. Skinned meshes
    /// are left out entirely - they carry no MeshFilter, their bounds live in bind space
    /// rather than in the object's, and what they hang lowest (a sail, a cape) is never the
    /// resting point.
    ///
    /// Only what the machines really SIMULATE is measured, which is a narrower thing than
    /// everything the prefab carries: a collider on a GameObject the prefab leaves inactive
    /// touches nothing and can never push a costume out of the ground, and one switched off is
    /// not in the solver's shapes either. Items ship such a spare all the same - CookedEgg and
    /// MushroomYellow each carry a second mesh, "pie (1)", with a collider fifty metres below
    /// their own pivot - and counting those asked for the lift of a costume a metre deep: the
    /// cap, every time, with the costume floating over the ground it should rest on.
    /// </summary>
    private static Vector2 Measure(GameObject prefab, out float meshLowest)
    {
        float lowest = 0f;
        meshLowest = 0f;
        float reach = 0f;
        Transform root = prefab.transform;
        Matrix4x4 toRoot = root.worldToLocalMatrix;

        foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
        {
            if (!CreaturePreview.SwitchedOn(child))
            {
                continue;
            }
            Matrix4x4 local = toRoot * child.localToWorldMatrix;

            Collider[] colliders = child.GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (!colliders[i].enabled)
                {
                    continue;
                }
                if (
                    !TryShape(
                        colliders[i],
                        out Vector3 centre,
                        out Vector3 half,
                        out bool flatBottom
                    )
                )
                {
                    continue;
                }
                float y = Lowest(local, centre, half);
                if (y < lowest)
                {
                    lowest = y;
                }
                if (flatBottom)
                {
                    float radius = Reach(local, centre, half);
                    if (radius > reach)
                    {
                        reach = radius;
                    }
                }
            }

            MeshFilter[] filters = child.GetComponents<MeshFilter>();
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh? mesh = filters[i].sharedMesh;
                if (mesh == null)
                {
                    continue;
                }
                float y = Lowest(local, mesh.bounds.center, mesh.bounds.extents);
                if (y < meshLowest)
                {
                    meshLowest = y;
                }
            }
        }

        return new Vector2(lowest, reach);
    }

    /// <summary>
    /// The local box of a collider: centre and half extents, and whether its bottom is flat
    /// (a box, a mesh hull - it meets the ground along a line or an area) or round (a sphere,
    /// a capsule - it meets it at a point). Anything else - a terrain, a wheel collider of
    /// its own shape - is simply not measured.
    /// </summary>
    private static bool TryShape(
        Collider collider,
        out Vector3 centre,
        out Vector3 half,
        out bool flatBottom
    )
    {
        if (collider is BoxCollider box)
        {
            centre = box.center;
            half = box.size * 0.5f;
            flatBottom = true;
            return true;
        }
        if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
        {
            Bounds bounds = meshCollider.sharedMesh.bounds;
            centre = bounds.center;
            half = bounds.extents;
            flatBottom = true;
            return true;
        }
        if (collider is SphereCollider sphere)
        {
            centre = sphere.center;
            half = Vector3.one * sphere.radius;
            flatBottom = false;
            return true;
        }
        if (collider is CapsuleCollider capsule)
        {
            float radius = capsule.radius;
            float length = Mathf.Max(capsule.height * 0.5f, radius);
            centre = capsule.center;
            half = new Vector3(radius, radius, radius);
            if (capsule.direction == 0)
            {
                half.x = length;
            }
            else if (capsule.direction == 1)
            {
                half.y = length;
            }
            else
            {
                half.z = length;
            }
            flatBottom = false;
            return true;
        }
        centre = default;
        half = default;
        flatBottom = false;
        return false;
    }

    /// <summary>Lowest corner of the box, transformed into the costume's own frame.</summary>
    private static float Lowest(Matrix4x4 local, Vector3 centre, Vector3 half)
    {
        float lowest = float.MaxValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 point = local.MultiplyPoint3x4(Corner(centre, half, i));
            if (point.y < lowest)
            {
                lowest = point.y;
            }
        }
        return lowest;
    }

    /// <summary>Farthest corner of the box from the costume's vertical axis.</summary>
    private static float Reach(Matrix4x4 local, Vector3 centre, Vector3 half)
    {
        float reach = 0f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 point = local.MultiplyPoint3x4(Corner(centre, half, i));
            float radius = Mathf.Sqrt(point.x * point.x + point.z * point.z);
            if (radius > reach)
            {
                reach = radius;
            }
        }
        return reach;
    }

    private static Vector3 Corner(Vector3 centre, Vector3 half, int index)
    {
        return new Vector3(
            centre.x + ((index & 1) == 0 ? -half.x : half.x),
            centre.y + ((index & 2) == 0 ? -half.y : half.y),
            centre.z + ((index & 4) == 0 ? -half.z : half.z)
        );
    }

    /// <summary>
    /// One line about the ground a costume is really being relayed over, whenever any part
    /// of the answer changes and at most every two seconds: the lift, the tilt, the height
    /// read under the player's own feet with the read that answered for it, and the part of
    /// the ground the plane could not explain. Built only after the clock has run out, so
    /// that switching the logs off - or leaving them on - costs no allocation at all between
    /// two lines.
    /// </summary>
    private static void Report(
        int prefabHash,
        float lift,
        Vector3 normal,
        float rise,
        float groundDelta,
        int source
    )
    {
        if (!DisguiseConfig.ConfigLogs.Value)
        {
            return;
        }
        if (Time.realtimeSinceStartup < s_nextReport)
        {
            return;
        }
        // The clock is spent whether or not anything is new: the line is built at most every
        // two seconds, never once per packet while nothing changes.
        s_nextReport = Time.realtimeSinceStartup + 2f;
        float tilt = Vector3.Angle(Vector3.up, normal);
        string sign = groundDelta >= 0f ? "+" : "";
        string ground = float.IsNaN(groundDelta)
            ? "no terrain read"
            : $"ground {sign}{groundDelta:F2} m vs feet ({SourceName(source)})";
        string message =
            $"Ground rest: {PrefabResolver.PrefabName(prefabHash)} is relayed {lift:F3} m above "
            + $"its player, laid {tilt:F1} degrees off upright | {ground}, rise {rise:F2} m";
        if (message == s_lastReport)
        {
            return;
        }
        s_lastReport = message;
        Disguise.LogInfo(message);
    }

    /// <summary>
    /// Which read answered, named the way the report prints it: the terrain layer, the probe
    /// that recognises terrain by its component, the heightmap the world was built from, or
    /// the zone built under a costume on a machine that had none standing there.
    /// </summary>
    private static string SourceName(int source)
    {
        if (source == ProbeSource)
        {
            return "probe";
        }
        if (source == HeightmapSource)
        {
            return "heightmap";
        }
        return source == ZoneSource ? "zone" : "ray";
    }

    /// <summary>
    /// The other half of the record: whether that tilt reached the bytes of the body being
    /// built. A report showing a slope with no line from here means the swap was refused
    /// (or measured for a ZDO that was never serialized), which a flat report would hide
    /// just as well as real flat ground. Logged when the angle changes, first time always.
    /// </summary>
    private static void ReportWritten(ZDO zdo, float degrees)
    {
        if (!DisguiseConfig.ConfigLogs.Value || Time.realtimeSinceStartup < s_nextSwapLog)
        {
            return;
        }
        if (!float.IsNaN(s_writtenDegrees) && Mathf.Abs(degrees - s_writtenDegrees) < 0.05f)
        {
            return;
        }
        s_writtenDegrees = degrees;
        s_nextSwapLog = Time.realtimeSinceStartup + 1f;
        Disguise.LogInfo(
            $"Ground rest: {PrefabResolver.PrefabName(zdo.GetPrefab())} rotation written into "
                + $"the packet: {degrees:F1} degrees off upright."
        );
    }
}
