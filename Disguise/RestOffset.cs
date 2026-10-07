#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Server, outgoing packet only: hands back the position of a disguised player raised to
/// where its costume actually RESTS, which is what stops that costume shivering on a machine
/// running no DLL of ours.
///
/// Why the shiver survives the band that freezes the packet (PositionHold): on the receiving
/// side ZSyncTransform turns gravity and velocity off, puts the body to sleep, and then -
/// every frame - drags it back to the relayed position with a 0.2 lerp as soon as it has
/// moved more than a millimetre. Whatever pushes the costume out of the ground therefore
/// comes straight back, and the two together make a cycle of a few centimetres: the raft the
/// game cannot tilt (it wears your upright rotation, so its uphill edge is buried in a
/// slope), the cart whose wheels are separate bodies on joints. Nothing else moves a body
/// configured that way - which is exactly what your tests showed: no shiver in water, none
/// in the air, none on your own clone (GhostFactory strips its rigidbody and colliders, so
/// it has no physics to lose the fight with).
///
/// The lift is measured from the costume's own geometry, once per prefab: how far below its
/// pivot anything hangs (the raft's hull, the cart's wheels), and how wide its flat bottom
/// is. It then adds the rise of the TERRAIN under that footprint - a flat bottom on a slope
/// is buried at the uphill edge however careful the pivot is - but only when the terrain is
/// what the player stands on: a floor, water or thin air are far below and a roof far above,
/// and none of them has a slope to correct.
///
/// Like the flight raise, this hands back a copy: the owner's zdo, the server's own state,
/// the saves and the game logic always keep the player on the ground. Patch_SendZDOs opens
/// the window around one ZDOMan.SendZDOs call; the same value is added on your own screen
/// (SelfView) so that both views of a costume agree to the centimetre.
/// </summary>
internal static class RestOffset
{
    /// <summary>Ray samples around the costume: the rise of a slope under its footprint.</summary>
    private const int RingSamples = 8;

    /// <summary>
    /// How far the terrain may be from the player's own feet for the two to be treated as
    /// resting on the same ground. Well below: a floor over it, water, air. Well above: the
    /// ray found a roof, and its height says nothing about what is under the feet.
    /// </summary>
    private const float GroundBand = 0.5f;

    /// <summary>
    /// Largest rise the footprint may buy: half a metre of it is already a costume hanging
    /// over a cliff edge, and past that the float would be worse than the shiver it replaces.
    /// </summary>
    private const float MaxFootprintRise = 0.5f;

    /// <summary>A reach under this is a round bottom: the ground there is the ground at the centre.</summary>
    private const float MinReach = 0.05f;

    /// <summary>
    /// Nothing may be lifted by more than this: a costume cannot need a metre to stop digging
    /// in, and a measurement asking for one has gone wrong (it is logged when it happens).
    /// </summary>
    private const float MaxLift = 1f;

    /// <summary>
    /// Measured once per costume, by prefab hash: x = its lowest point, never above the
    /// pivot; y = how far its flat bottom reaches sideways.
    /// </summary>
    private static readonly Dictionary<int, Vector2> s_footprints = new Dictionary<int, Vector2>();

    /// <summary>
    /// Costumes measured and deliberately left alone - whatever else is true of them, the
    /// machines rendering them are not in a fight with the ground (see FightsTheGround).
    /// </summary>
    private static readonly HashSet<int> s_leftAlone = new HashSet<int>();

    /// <summary>True while the positions this call asks for are landing in a packet.</summary>
    private static bool s_sending;

    /// <summary>Own view: the lift recomputed at most ten times a second (it costs rays).</summary>
    private static bool s_hasView;

    private static int s_viewHash;

    private static Vector3 s_viewPosition;

    private static float s_viewNext;

    private static float s_viewLift;

    /// <summary>Own view logging: the last lift reported and the earliest next report.</summary>
    private static float s_reportedLift = float.NaN;

    private static float s_nextReport;

    /// <summary>
    /// Opens the window for one ZDOMan.SendZDOs call. Only the relaying machine opens it: a
    /// client sending a raised position would have the server STORE it.
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
    /// Raises the copy ZDO.GetPosition is about to hand to the packet builder by what its
    /// costume needs to stop digging in. No-op outside the window, for a viking, and while
    /// the observer refresh fakes its far-away position - that one has to arrive as faked.
    /// </summary>
    internal static void Raise(ZDO zdo, ref Vector3 position)
    {
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
            float lift = Lift(hash, position);
            if (lift > 0f)
            {
                Report(hash, lift);
                position.y += lift;
            }
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Ground rest (outgoing position): {ex}");
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
            if (
                s_hasView
                && prefabHash == s_viewHash
                && Time.unscaledTime < s_viewNext
                && (position - s_viewPosition).sqrMagnitude < 0.04f
            )
            {
                return Vector3.up * s_viewLift;
            }
            s_hasView = true;
            s_viewHash = prefabHash;
            s_viewPosition = position;
            s_viewNext = Time.unscaledTime + 0.1f;
            s_viewLift = Lift(prefabHash, position);
            return Vector3.up * s_viewLift;
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Ground rest (own view): {ex}");
            return Vector3.zero;
        }
    }

    // ------------------------------------------------------------------ internals

    /// <summary>
    /// How much higher than its player this costume must be relayed: everything it hangs
    /// below the level the player stands on, plus the rise of the terrain under its footprint
    /// while the terrain is what they stand on.
    /// </summary>
    private static float Lift(int prefabHash, Vector3 position)
    {
        if (!Footprint(prefabHash, out Vector2 footprint))
        {
            return 0f;
        }

        float lift = Mathf.Max(0f, -footprint.x);

        ZoneSystem? zone = ZoneSystem.instance;
        if (zone != null && footprint.y > MinReach)
        {
            float ground = zone.GetGroundHeight(position);
            if (Mathf.Abs(ground - position.y) <= GroundBand)
            {
                float high = ground;
                for (int i = 0; i < RingSamples; i++)
                {
                    float angle = i * (2f * Mathf.PI / RingSamples);
                    Vector3 sample = position;
                    sample.x += Mathf.Cos(angle) * footprint.y;
                    sample.z += Mathf.Sin(angle) * footprint.y;
                    float height = zone.GetGroundHeight(sample);
                    if (height > high)
                    {
                        high = height;
                    }
                }
                lift += Mathf.Clamp(high - position.y, 0f, MaxFootprintRise);
            }
        }

        return lift > MaxLift ? MaxLift : lift;
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
        footprint = Measure(prefab);
        s_footprints[prefabHash] = footprint;
        Disguise.LogInfo(
            $"Ground rest: {PrefabResolver.PrefabName(prefabHash)} hangs "
                + $"{-footprint.x:F3} m below its pivot, flat bottom "
                + $"{footprint.y:F2} m wide."
        );
        return true;
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
    /// below its pivot anything hangs, and how far sideways its FLAT bottom reaches - the
    /// part that can be inside a slope. A sphere or a capsule is not counted as flat: it
    /// touches the ground at one point, and the ground there is the ground at the centre.
    ///
    /// Meshes are only ever asked for their lowest point, never for their reach: a canopy
    /// three metres wide is not what the costume rests on. Skinned meshes are left out
    /// entirely - they carry no MeshFilter, their bounds live in bind space rather than in
    /// the object's, and what they hang lowest (a sail, a cape) is never the resting point.
    /// </summary>
    private static Vector2 Measure(GameObject prefab)
    {
        float lowest = 0f;
        float reach = 0f;
        Transform root = prefab.transform;
        Matrix4x4 toRoot = root.worldToLocalMatrix;

        foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
        {
            Matrix4x4 local = toRoot * child.localToWorldMatrix;

            Collider[] colliders = child.GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
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
                if (y < lowest)
                {
                    lowest = y;
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
    /// One line about the lift a costume is really getting, when it changes by more than two
    /// centimetres and at most every two seconds: the number to read against the shiver it
    /// replaces, and the alarm for a measurement going wrong. Nothing is built for this when
    /// the logs are off.
    /// </summary>
    private static void Report(int prefabHash, float lift)
    {
        if (!DisguiseConfig.ConfigLogs.Value)
        {
            return;
        }
        if (Time.realtimeSinceStartup < s_nextReport)
        {
            return;
        }
        if (!float.IsNaN(s_reportedLift) && Mathf.Abs(lift - s_reportedLift) < 0.02f)
        {
            return;
        }
        s_reportedLift = lift;
        s_nextReport = Time.realtimeSinceStartup + 2f;
        Disguise.LogInfo(
            $"Ground rest: {PrefabResolver.PrefabName(prefabHash)} is relayed {lift:F3} m above its player."
        );
    }
}
