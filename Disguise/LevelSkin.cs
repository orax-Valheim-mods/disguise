#nullable enable
using System;
using System.Reflection;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Puts the skin of a star level on a copy WE built: the ghost your own screen draws
/// (SelfView) and the picture in the window (CreaturePreview) are inert copies, and the
/// game's own skin cannot dress them. LevelEffects puts it on in Start, through the
/// Character above it, and GhostFactory strips every Character from a copy before it wakes.
///
/// So this calls the very method the game calls - SetupLevelVisualization, which draws the
/// level: its scale, its alternate skin object, the tinted material - and lends the copy the
/// one thing the method asks of its Character: the prefab's OWN, which is exactly what that
/// field means, and of which the method reads nothing but the name of its gameObject (the
/// key of the material cache, shared with the real creatures of the world).
///
/// Everything goes through reflection BY NAME, the way HealthMask reads m_health: the game's
/// members stay out of the count the README documents, and a rename in a game update costs
/// the base look - never an exception on your screen.
/// </summary>
internal static class LevelSkin
{
    /// <summary>The two members looked up once, the same on every creature of the game.</summary>
    private static FieldInfo? s_characterField;

    private static MethodInfo? s_setup;

    /// <summary>
    /// Dresses the copy as a creature of <paramref name="level"/>. A level of 1 has no skin
    /// to put on (the game draws nothing above the base look), and most costumes are not
    /// creatures at all - both cost one early return and nothing else.
    /// </summary>
    internal static void Apply(GameObject? copy, GameObject? prefab, int level)
    {
        if (copy == null || prefab == null || level <= 1)
        {
            return;
        }
        string prefabName = prefab.name;
        try
        {
            // By NAME, among the children however inactive: the string overload of
            // GetComponentInChildren does not reach into an inactive copy (and the typed one
            // would be a game type in the README's count). The component is ACTIVE on every
            // real creature - Start is what dresses it - but the copy is built inert.
            Component? effects = null;
            foreach (Component component in copy.GetComponentsInChildren<Component>(true))
            {
                if (component != null && component.GetType().Name == "LevelEffects")
                {
                    effects = component;
                    break;
                }
            }
            if (effects == null)
            {
                return;
            }
            Type type = effects.GetType();
            s_characterField ??= type.GetField(
                "m_character",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            Component? character = prefab.GetComponent("Character");
            if (
                s_characterField != null
                && character != null
                && s_characterField.FieldType.IsInstanceOfType(character)
            )
            {
                s_characterField.SetValue(effects, character);
            }
            s_setup ??= type.GetMethod("SetupLevelVisualization", new[] { typeof(int) });
            s_setup?.Invoke(effects, new object[] { level });
        }
        catch (Exception ex)
        {
            // A copy without its skin is still a copy: the world keeps drawing the costume.
            Disguise.LogWarning($"Level skin of {prefabName} ({level}): {ex.Message}");
        }
    }
}
