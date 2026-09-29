#nullable enable
using System;
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
