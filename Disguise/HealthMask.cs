#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Server, outgoing packet only: a machine running no DLL of ours reads the health of your
/// costume out of YOUR zdo - WearNTear asks it with a fallback on the prefab's own maximum,
/// so a piece of furniture wearing you shows 100 hit points against its 500: broken planks
/// on a cart, a sail torn off a raft, where the clone your own screen draws is brand new.
/// (A creature reads the same key as a Humanoid, and gets your bar at the creature's scale.)
///
/// For the duration of ZDO.Serialize this therefore writes the costume's own maximum into
/// that key and puts yours back the moment the packet exists: the swap does not bump the
/// DataRevision, never reaches this machine's own state, and ZDO.Save - the path the world
/// file reads - does not go through Serialize at all.
///
/// Only the RELAYING machine swaps. An owner serializing its own zdo would push the
/// costume's maximum at the server, and ZDO.Deserialize stores every key a packet carries:
/// your real hit points would be replaced for good.
///
/// Everyone else is then shown a whole costume - vanilla clients and modded ones alike,
/// there being no per-peer packet (Patch_SendZDOs has the reason). Nobody but you and the
/// server can tell how hurt you are through a disguise any more; that is the price of a
/// costume that no longer looks wrecked on the machines you walk in front of.
/// </summary>
internal static class HealthMask
{
    /// <summary>The game's own key: WearNTear and Humanoid both read it.</summary>
    private static readonly int s_healthKey = StringExtensionMethods.GetStableHashCode("health");

    /// <summary>
    /// The components that own a prefab's hit points, in the order they are tried: a piece or
    /// a vehicle through WearNTear, a creature or a viking through Humanoid. Read by NAME on
    /// the root, the same Unity overload PrefabCatalog uses for ZSyncTransform - both are
    /// root components of every costume that carries them, and a typed
    /// GetComponent&lt;Humanoid&gt;() would be one more game member than the count documented
    /// in the README.
    /// </summary>
    private static readonly string[] s_healthOwners = { "WearNTear", "Humanoid" };

    /// <summary>
    /// Maximum hit points of every costume already measured, by prefab hash (0: measured and
    /// without any - a dropped item, which keeps your own value and shows no damage anyway).
    /// </summary>
    private static readonly Dictionary<int, float> s_maximum = new Dictionary<int, float>();

    // Active swap window: the game is single threaded and ZDO.Serialize never nests, but the
    // same rule as EquipmentMask applies - End() only ever closes what Begin opened.
    private static ZDO? s_activeZdo;

    private static float s_saved;

    /// <summary>
    /// ZDO.Serialize: puts the costume's maximum in the health key for the duration of the
    /// packet. Returns true when something was actually swapped - the caller must then call
    /// End().
    /// </summary>
    internal static bool BeginOutgoingHealth(ZDO? zdo)
    {
        if (s_activeZdo != null || zdo == null || !zdo.IsValid())
        {
            return false;
        }
        // Runs for every single zdo of an outgoing packet: the marker is the cheap test.
        if (!DisguiseApplier.HasMarker(zdo))
        {
            return false;
        }
        // What the others receive may only be rewritten here, on the machine that relays it -
        // see the class note: the owner must keep sending its real value.
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            return false;
        }
        float real = zdo.GetFloat(s_healthKey, -1f);
        if (real <= 0f)
        {
            return false; // nothing stored, so nothing that could ever be put back
        }
        float maximum = Maximum(zdo);
        if (maximum <= 0f || real >= maximum)
        {
            return false; // no maximum found for this costume, or the costume is already whole
        }

        s_activeZdo = zdo;
        s_saved = real;
        try
        {
            GameAccess.SetZdoFloatSilent(zdo, s_healthKey, maximum);
        }
        catch (Exception ex)
        {
            End();
            Disguise.LogError($"Health mask (outgoing): {ex}");
            return false;
        }
        return true;
    }

    /// <summary>Writes the saved value back: called by the Finalizer, whatever happened.</summary>
    internal static void End()
    {
        ZDO? zdo = s_activeZdo;
        float saved = s_saved;
        s_activeZdo = null;
        s_saved = 0f;
        if (zdo == null)
        {
            return;
        }
        try
        {
            GameAccess.SetZdoFloatSilent(zdo, s_healthKey, saved);
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Health mask (restore): {ex}");
        }
    }

    // ------------------------------------------------------------------ internals

    /// <summary>The costume's own hit points, measured once per prefab.</summary>
    private static float Maximum(ZDO zdo)
    {
        int hash = zdo.GetPrefab();
        if (s_maximum.TryGetValue(hash, out float cached))
        {
            return cached;
        }
        float maximum = 0f;
        ZNetScene? zns = ZNetScene.instance;
        GameObject? prefab = zns != null ? zns.GetPrefab(hash) : null;
        if (prefab != null)
        {
            maximum = ReadMaximum(prefab);
        }
        s_maximum[hash] = maximum;
        return maximum;
    }

    /// <summary>
    /// The maximum hit points of a costume, read from the component that owns them: by name
    /// for the lookup and through reflection for the field, which keeps this out of the game
    /// assembly's member count (audit_visibility). Nothing to return means no swap, never a
    /// guess.
    /// </summary>
    private static float ReadMaximum(GameObject prefab)
    {
        foreach (string name in s_healthOwners)
        {
            Component? owner = prefab.GetComponent(name);
            if (owner == null)
            {
                continue;
            }
            FieldInfo? field = owner.GetType().GetField("m_health");
            if (
                field != null
                && field.FieldType == typeof(float)
                && field.GetValue(owner) is float value
                && value > 0f
            )
            {
                return value;
            }
        }
        return 0f;
    }
}
