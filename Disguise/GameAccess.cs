#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Disguise;

/// <summary>
/// The only place that touches non-public game members: reflection handles resolved once
/// at load time, and the small helpers built on them (Character.m_nview, ZDO.DataRevision).
/// </summary>
internal static class GameAccess
{
    // -- Reflection access to the game's NON PUBLIC members ------------------------------
    // In the game's (original) assembly these members are private/protected: any direct
    // reference would throw a FieldAccessException at runtime. Only the "publicized" copy
    // (valheim_Data\Managed\publicized_assemblies) used for compilation exposes them as
    // public - hence this mandatory reflection layer.
    internal static readonly FieldInfo? s_zdoPrefabField = typeof(ZDO).GetField(
        "m_prefab",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
    );

    internal static readonly FieldInfo? s_characterNViewField = typeof(Character).GetField(
        "m_nview",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
    );

    internal static readonly MethodInfo? s_increaseDataRevisionMethod = typeof(ZDO).GetMethod(
        "IncreaseDataRevision",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
    );

    // ZDO.m_position: private in the game. Written (and immediately restored) only while
    // ZDOMan.SendZDOs builds an outgoing packet: the ZDO state itself never changes.
    internal static readonly FieldInfo? s_zdoPositionField = typeof(ZDO).GetField(
        "m_position",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
    );

    // VisEquipment.m_nview: private in the game. The render patch needs it to reach the ZDO
    // the equipment hashes are read from (EquipmentMask).
    internal static readonly FieldInfo? s_visEquipmentNViewField = typeof(VisEquipment).GetField(
        "m_nview",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
    );

    // ZDOExtraData.s_saveInts: private in the game. ZDO.Save does NOT read the live values,
    // it reads the snapshot cloned by ZDOExtraData.PrepareSave() at the start of the save.
    internal static readonly FieldInfo? s_saveIntsField = typeof(ZDOExtraData).GetField(
        "s_saveInts",
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
    );

    // FootStep.FindBestStepEffect: private in the game. Resolves an index into THAT instance's
    // own m_effects list, which is what the disguise has to translate (see
    // Patch_FootStepFindBestStepEffect). Invoked on the creature prefab's component: its Start
    // never ran, but the method only reads the list.
    internal static readonly MethodInfo? s_footStepFindBestMethod = typeof(FootStep).GetMethod(
        "FindBestStepEffect",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
    );

    /// <summary>
    /// ZNetView of a character (Character.m_nview is "protected" -&gt; reflection).
    /// </summary>
    internal static ZNetView? GetNView(Character? character)
    {
        if (character == null || s_characterNViewField == null)
        {
            return null;
        }
        try
        {
            return s_characterNViewField.GetValue(character) as ZNetView;
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Cannot read Character.m_nview: {ex.Message}");
            return null;
        }
    }

    /// <summary>ZDO of the local player, or null (main menu / dedicated server without a player).</summary>
    internal static ZDO? GetLocalPlayerZDO()
    {
        ZNetView? nview = GetNView(Player.m_localPlayer);
        return nview != null ? nview.GetZDO() : null;
    }

    /// <summary>
    /// ZDO a VisEquipment renders for, or null when it has no network view yet
    /// (VisEquipment.m_nview is private -&gt; reflection).
    /// </summary>
    internal static ZDO? GetZDO(VisEquipment? visEquipment)
    {
        if (visEquipment == null || s_visEquipmentNViewField == null)
        {
            return null;
        }
        try
        {
            return (s_visEquipmentNViewField.GetValue(visEquipment) as ZNetView)?.GetZDO();
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Cannot read VisEquipment.m_nview: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Index of the best step effect of this FootStep for the (material, motion) pair of the
    /// original call, or -1 when nothing matches or the call could not be made. The arguments
    /// are handed over exactly as Harmony received them: the parameter types are FootStep's own
    /// enums, which this project never references directly.
    /// </summary>
    internal static int FindBestStepEffect(FootStep? step, object[]? args)
    {
        if (step == null || s_footStepFindBestMethod == null)
        {
            return -1;
        }
        try
        {
            return s_footStepFindBestMethod.Invoke(step, args) is int index ? index : -1;
        }
        catch (Exception ex)
        {
            Disguise.LogError($"Cannot call FootStep.FindBestStepEffect: {ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// Writes a ZDO int WITHOUT bumping the DataRevision: the value only changes in the
    /// local copy and never reaches the network (ZDO.Set would bump it, which re-sends the
    /// whole ZDO). Used by EquipmentMask to swap values around a packet / a frame.
    /// </summary>
    internal static void SetZdoIntSilent(ZDO zdo, int key, int value)
    {
        // ZDOExtraData.Set(ZDOID, int, int) is public in the original assembly: it only
        // writes the value, the revision bump lives in ZDO.Set.
        ZDOExtraData.Set(zdo.m_uid, key, value);
    }

    /// <summary>
    /// Writes an int into the snapshot ZDO.Save reads (the copy cloned by
    /// ZDOExtraData.PrepareSave, not the live values). That snapshot is thrown away by
    /// ClearSave() at the end of the save: no restore is needed, and the live ZDO state -
    /// network, saves in flight, your own view - is untouched.
    /// </summary>
    internal static void SetSaveInt(ZDO zdo, int key, int value)
    {
        if (s_saveIntsField == null)
        {
            return;
        }
        if (
            s_saveIntsField.GetValue(null)
            is Dictionary<ZDOID, BinarySearchDictionary<int, int>> saveInts
        )
        {
            // ZDOHelper.InitAndSet: creates the entry when the ZDO has none yet.
            saveInts.InitAndSet(zdo.m_uid, key, value);
        }
    }

    /// <summary>
    /// Bumps the ZDO DataRevision so that the change reaches the server
    /// (ZDOMan.ClientChanged -&gt; RPC_ZDOData). ZDO.IncreaseDataRevision is private in the
    /// game: called through reflection.
    /// </summary>
    internal static void BumpDataRevision(ZDO zdo)
    {
        if (s_increaseDataRevisionMethod != null)
        {
            s_increaseDataRevisionMethod.Invoke(zdo, null);
            return;
        }
        // Public fallback: ZDO.Set(int, int) bumps the DataRevision by itself.
        zdo.Set(PrefabResolver.MarkerHash, 0);
    }
}
