#nullable enable
using System;
using System.Collections.Generic;

namespace Disguise;

/// <summary>
/// The favorite prefabs of the window: their names held as ONE comma-separated line in the
/// config ([6 - GUI] Favorites), read through Contains and rewritten by Toggle. The line is
/// parsed only when it is not the one the two lists were built from - the raw string is
/// compared first - so Contains is free where it is asked often: the label of the Favorite
/// button, once per frame, and the list's filter, once per row.
///
/// A favorite is a MARK on a prefab and never a category of it. The kind PrefabCatalog gave
/// the prefab stands (PrefabKind stays exactly one per prefab, and the six kind boxes keep
/// their six Show* entries), and the first box of GuiCategories - Favorites, which is a
/// Show* of its own - is a filter beside the kinds: while it is on, the marks are what the
/// list shows and the six kinds are ignored (GuiCategories.ShowRow).
///
/// The names are spelled the way the list spells them - the vanilla catalogue, exactly what
/// "disguise list" prints. A name typed by hand that no catalogue holds never matches a row,
/// and Toggle rewrites only what it was given: names already in the line keep their order,
/// and nothing is dropped from the config behind the user's back.
/// </summary>
internal static class PrefabFavorites
{
    /// <summary>What stands between two names: no vanilla prefab name holds one.</summary>
    private const char Separator = ',';

    /// <summary>The line the two lists below were parsed from (empty before the first read).</summary>
    private static string s_line = string.Empty;

    /// <summary>The names in the order the config holds them, and the same names for lookups.</summary>
    private static readonly List<string> s_order = new();

    private static readonly HashSet<string> s_set = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether this prefab is one of the favorites (case is not another name).</summary>
    internal static bool Contains(string name)
    {
        if (name.Length == 0)
        {
            return false;
        }
        Parse();
        return s_set.Contains(name);
    }

    /// <summary>
    /// Marks a prefab, or takes the mark off, and answers which of the two it became. The
    /// line is written back whole (the config is the only storage there is), keeping the
    /// names it already held and their order: a mark added goes last, and the name taken off
    /// goes out of every place it was repeated. The window follows through GuiCategories.Sync,
    /// which reads the line every frame while it is open.
    /// </summary>
    internal static bool Toggle(string name)
    {
        if (name.Length == 0)
        {
            return false;
        }

        Parse();
        bool added;
        if (s_set.Remove(name))
        {
            added = false;
            for (int i = s_order.Count - 1; i >= 0; i--)
            {
                if (string.Equals(s_order[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    s_order.RemoveAt(i);
                }
            }
        }
        else
        {
            added = true;
            s_order.Add(name);
            _ = s_set.Add(name);
        }

        // string.Join takes no char on this framework, hence the same comma as text.
        string line = string.Join(",", s_order);
        s_line = line;
        DisguiseConfig.ConfigFavorites.Value = line;
        return added;
    }

    /// <summary>
    /// Builds the two lists from the config line, when the line is not the one they hold -
    /// a write of the button, a value typed in ConfigurationManager and an edit of the .cfg
    /// all three arrive here as a new string on the entry. Empty pieces are skipped and a
    /// repeated name is kept in the line (it costs a second entry there, and never a wrong
    /// answer: the set is what Contains asks).
    /// </summary>
    private static void Parse()
    {
        string line = DisguiseConfig.ConfigFavorites.Value ?? string.Empty;
        if (string.Equals(line, s_line, StringComparison.Ordinal))
        {
            return;
        }

        s_line = line;
        s_order.Clear();
        s_set.Clear();
        string[] pieces = line.Split(Separator);
        for (int i = 0; i < pieces.Length; i++)
        {
            string name = pieces[i].Trim();
            if (name.Length == 0)
            {
                continue;
            }
            s_order.Add(name);
            _ = s_set.Add(name);
        }
    }
}
