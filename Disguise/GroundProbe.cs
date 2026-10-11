#nullable enable
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Disguise;

/// <summary>
/// The terrain a costume can be read from on a machine that has none where the player
/// stands.
///
/// A dedicated server only spawns real zone roots - the Heightmap, the mesh and the collider
/// a tile of terrain is made of - around its own reference position: the zones a player
/// walks in are ghost-generated, which places the world data and then destroys the temporary
/// root again. Nothing is left standing there, so the terrain-layer ray meets no collider at
/// all, the probe has no Heightmap to recognise, and Heightmap.GetHeight finds no map
/// covering the point: three reads, three silences, and a costume relayed at its player's
/// own height - horizontal, and buried on any slope.
///
/// The fix is to build what the server never built, the way the game itself builds it: the
/// zone prefab at the zone position, guarded by HeightmapBuilder.IsTerrainReady exactly as
/// ZoneSystem.SpawnZone guards it. That guard does not only ask - a tile nobody has queued is
/// queued BY the question - so asking on every read is what gets the terrain built, and only
/// a true may be obeyed, because instantiating a zone whose data is still missing would
/// block the sending thread. Nothing of the game's own bookkeeping is used: the root is never
/// registered in ZoneSystem.m_zones, so the game never learns about it and its TTL never
/// touches it either.
///
/// A tile still being built is waited for, which is what makes the read exact rather than
/// merely eventual: the terrain data a tile is built FROM is consumed by that build, and the
/// server has already spent this zone's data once - the ghost pass built it, instantiated it
/// and destroyed it - so it must be built again before anything can stand on it. The game
/// makes the same wait in HeightmapBuilder.RequestTerrainSync, on a spin with no end; this
/// one is bounded, and only one may be open at a time, because the alternative is a packet
/// leaving without the ground it rests on and staying wrong until the costume changes again.
///
/// The heightmap registers itself on Awake, so from the moment the root exists the ordinary
/// reads find it like any other terrain - the ray hits its collider, Heightmap.GetHeight
/// finds its map. What this class answers is therefore the FIRST read of a zone; every read
/// after that touches the zone under it (one dictionary lookup) and is answered by those
/// ordinary reads instead, and a zone no read has touched for a while is destroyed again.
///
/// The heights come from the very tile a client would stand on - world data and terrain
/// modifiers alike - which reading the world generator would not give: a costume has to rest
/// on the surface the observing machine renders, sculpted or cleared alike.
/// </summary>
internal static class GroundProbe
{
    /// <summary>
    /// How long a zone built for a read may go unread before it is dropped again: long
    /// enough that a costume standing still keeps its terrain - every ground read touches
    /// the zone under it - and short enough that walking away leaves nothing behind.
    /// </summary>
    private const float ProbeSeconds = 10f;

    /// <summary>How often unread zones are swept: once a read, never once per sample.</summary>
    private const float TrimSeconds = 5f;

    /// <summary>
    /// How long a packet may wait for the terrain of the zone it is reading: a tile builds
    /// in about ten to thirty milliseconds (the builder sleeps between two jobs), and a read
    /// that gives up instead costs the receiving machine a position WITHOUT the ground - one
    /// the game will not send again until the costume itself changes, so the wrong position
    /// is not a wrong frame but a wrong minute.
    /// </summary>
    private const float BuildWaitSeconds = 0.1f;

    /// <summary>
    /// The zones built here, by zone: the root, the heightmap it carries, and the time any
    /// read last touched that zone. A value tuple rather than a type of its own - the
    /// project keeps one type per file.
    /// </summary>
    private static readonly Dictionary<
        Vector2Int,
        (GameObject Root, Component Map, float Used)
    > s_zones = new Dictionary<Vector2Int, (GameObject Root, Component Map, float Used)>();

    /// <summary>
    /// What the last sweep found unread, so the dictionary is not edited while it is being
    /// walked. Cleared with it: both are reused, a ground read may not cost an allocation.
    /// </summary>
    private static readonly List<Vector2Int> s_stale = new List<Vector2Int>();

    private static float s_nextTrim;

    /// <summary>
    /// When the one build wait that may be open closes: a frame asking for several zones at
    /// once spends one budget rather than one per costume, so a send pass stalls once, not
    /// once per costume in it.
    /// </summary>
    private static float s_waitUntil;

    /// <summary>Set once, so that the line saying this machine builds terrain is one line.</summary>
    private static bool s_announced;

    /// <summary>Why the last attempt to build a zone failed - the self-test prints it.</summary>
    private static string? s_lastFailure;

    /// <summary>
    /// Keeps a zone that was already built under this read alive for another while. Called
    /// for EVERY ground read rather than only for the ones that end here: once the zone
    /// exists the ray answers from its collider, and this is what still tells this class the
    /// terrain is wanted. Touching a zone nobody built costs one dictionary lookup and builds
    /// nothing.
    /// </summary>
    internal static void Touch(Vector3 position)
    {
        Vector2Int zone = Zone(position);
        if (s_zones.TryGetValue(zone, out (GameObject Root, Component Map, float Used) found))
        {
            if (found.Root == null || found.Map == null)
            {
                // Dropped from under us by a world change: nothing to keep alive.
                s_zones.Remove(zone);
            }
            else
            {
                s_zones[zone] = (found.Root, found.Map, Time.realtimeSinceStartup);
            }
        }
        Sweep();
    }

    /// <summary>
    /// The height under a point from the zone built for it, building that zone when nothing
    /// else on this machine has terrain to offer there - including when the one that used to
    /// stand there was dropped by a world change. False with the reason left for
    /// <see cref="LastFailure"/> when even the wait for its data gives nothing, so the next
    /// read asks again.
    /// </summary>
    internal static bool TryGetHeight(Vector3 position, out float height)
    {
        height = 0f;
        Vector2Int zone = Zone(position);
        if (
            !s_zones.TryGetValue(zone, out (GameObject Root, Component Map, float Used) found)
            || found.Root == null
            || found.Map == null
        )
        {
            // Never built, or destroyed from under us: both are answered by building it now
            // rather than by waiting for a later read that may never come.
            s_zones.Remove(zone);
            if (!TryBuild(zone, out found))
            {
                return false;
            }
        }
        if (!GameAccess.TryGetHeightmapHeight(found.Map, position, out height))
        {
            s_lastFailure = "height read failed";
            return false;
        }
        s_lastFailure = null;
        return true;
    }

    /// <summary>What the last attempt to build a zone said, for the self-test line.</summary>
    internal static string LastFailure()
    {
        return s_lastFailure ?? "not asked";
    }

    /// <summary>
    /// Builds the zone the game would have built here: the zone prefab at the zone position,
    /// and - once <see cref="WaitForTerrain"/> has seen its data there - nothing else. The
    /// root is not registered in ZoneSystem.m_zones - the game neither needs to know, nor
    /// gets the chance to expire it - while the heightmap registers ITSELF on Awake, which is
    /// what makes the ordinary reads find it afterwards.
    /// </summary>
    private static bool TryBuild(
        Vector2Int zone,
        out (GameObject Root, Component Map, float Used) built
    )
    {
        built = default;
        if (!GameAccess.HasZoneTerrainAccess())
        {
            s_lastFailure = "reflection unresolved";
            return false;
        }
        GameObject? prefab = GameAccess.GetZonePrefab();
        if (prefab == null)
        {
            s_lastFailure = "zone prefab unresolved";
            return false;
        }
        Vector3 zonePosition = ZonePosition(zone);
        Component? prefabMap = GameAccess.GetHeightmap(prefab);
        if (prefabMap == null)
        {
            s_lastFailure = "zone has no heightmap";
            return false;
        }
        if (!WaitForTerrain(zonePosition, prefabMap))
        {
            return false;
        }
        GameObject root = UnityEngine.Object.Instantiate(prefab, zonePosition, Quaternion.identity);
        Component? map = GameAccess.GetHeightmap(root);
        if (map == null)
        {
            UnityEngine.Object.Destroy(root);
            s_lastFailure = "zone has no heightmap";
            return false;
        }
        built = (root, map, Time.realtimeSinceStartup);
        s_zones[zone] = built;
        if (!s_announced)
        {
            s_announced = true;
            Disguise.LogInfo(
                $"Ground rest: no terrain instance stands where the costume is read, "
                    + $"so the zone at ({zonePosition.x:F0}, {zonePosition.z:F0}) is built to read it."
            );
        }
        return true;
    }

    /// <summary>
    /// True when the tile's terrain data is there to instantiate - waiting for it when the
    /// question has just queued it, because the packet this read belongs to may not leave
    /// without it: the game sends a costume again only when the costume itself changes, so a
    /// position written without its ground is not a wrong frame but a wrong minute. The game
    /// makes the same wait in HeightmapBuilder.RequestTerrainSync, on a spin that ends only
    /// when the data appears; this one has a deadline (the builder may be gone) and only one
    /// may be open at a time, so a frame meeting several unbuilt zones spends one budget
    /// rather than one per costume.
    /// </summary>
    private static bool WaitForTerrain(Vector3 zonePosition, Component prefabMap)
    {
        float now = Time.realtimeSinceStartup;
        // Whoever asks while another wait is open does not wait at all: one poll, and the
        // answer that one gives is the answer it keeps.
        bool budget = now >= s_waitUntil;
        if (budget)
        {
            s_waitUntil = now + BuildWaitSeconds;
        }
        float deadline = budget ? s_waitUntil : now;
        while (true)
        {
            bool? ready = GameAccess.IsZoneTerrainReady(zonePosition, prefabMap);
            if (ready == null)
            {
                s_lastFailure = "reflection unresolved";
                return false;
            }
            if (ready.Value)
            {
                return true;
            }
            if (Time.realtimeSinceStartup >= deadline)
            {
                s_lastFailure = budget ? "terrain build timed out" : "another zone is building";
                return false;
            }
            Thread.Sleep(1);
        }
    }

    /// <summary>
    /// Destroys the zones no read has touched for a while - the root goes, and with it the
    /// heightmap the game had registered on its own. Throttled by <see cref="TrimSeconds"/>,
    /// so a read costs a comparison rather than a walk over the dictionary.
    /// </summary>
    private static void Sweep()
    {
        float now = Time.realtimeSinceStartup;
        if (now < s_nextTrim)
        {
            return;
        }
        s_nextTrim = now + TrimSeconds;
        s_stale.Clear();
        foreach (
            KeyValuePair<Vector2Int, (GameObject Root, Component Map, float Used)> entry in s_zones
        )
        {
            if (now - entry.Value.Used > ProbeSeconds)
            {
                s_stale.Add(entry.Key);
            }
        }
        for (int i = 0; i < s_stale.Count; i++)
        {
            if (
                s_zones.TryGetValue(
                    s_stale[i],
                    out (GameObject Root, Component Map, float Used) found
                )
            )
            {
                s_zones.Remove(s_stale[i]);
                if (found.Root != null)
                {
                    // Heightmap.OnDestroy takes it back out of the game's own map list.
                    UnityEngine.Object.Destroy(found.Root);
                }
            }
        }
        s_stale.Clear();
    }

    /// <summary>
    /// The plugin is unloading (ScriptEngine reloads it into a NEW assembly, statics and all):
    /// every zone root built here goes with this copy, or the game keeps a Heightmap it had
    /// registered on its own for a tile the copy loaded next builds a SECOND one for - two
    /// heightmaps on one tile, and the ground reads answering from either of them, one more
    /// orphan per reload. Sweep cannot be the one doing this job: it only runs from a READ,
    /// and an unload comes precisely when there is none left to make.
    /// </summary>
    internal static void Cleanup()
    {
        foreach (
            KeyValuePair<Vector2Int, (GameObject Root, Component Map, float Used)> entry in s_zones
        )
        {
            if (entry.Value.Root != null)
            {
                // Heightmap.OnDestroy takes it back out of the game's own map list.
                UnityEngine.Object.Destroy(entry.Value.Root);
            }
        }
        s_zones.Clear();
    }

    /// <summary>
    /// The zone a point lies in: the body of ZoneSystem.GetZone, whose 64 m tile and +32 are
    /// hard-coded there. Replicated rather than called because the audit counts every member
    /// reference, public or not - and because the game's own position for that zone is the
    /// tile's CENTRE, which the +32 is what makes.
    /// </summary>
    private static Vector2Int Zone(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt((float)(((double)position.x + 32.0) / 64.0)),
            Mathf.FloorToInt((float)(((double)position.z + 32.0) / 64.0))
        );
    }

    /// <summary>
    /// Where the terrain of that zone stands: ZoneSystem.GetZonePos, which multiplies the
    /// zone by the same 64 and leaves the height at zero (the world's own Y).
    /// </summary>
    private static Vector3 ZonePosition(Vector2Int zone)
    {
        return new Vector3(zone.x * 64f, 0f, zone.y * 64f);
    }
}
