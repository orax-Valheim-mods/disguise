#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Disguise;

/// <summary>
/// Answers "does this prefab ship with the game?" - the filter that keeps the two lists
/// (disguise list and the window) to vanilla prefabs, without a name list of our own to
/// carry and to re-generate after every game update.
///
/// Valheim describes its own assets in two plain-text files under
/// valheim_Data\StreamingAssets\SoftRef: manifest (229 KB) and manifest_extended (3.3 MB),
/// one "path in bundle: Assets/.../Ghost.prefab" line per asset. A mod never lands in them:
/// it loads its bundle from its own plugin folder through AssetBundle.LoadFromFile, which
/// the game never indexes. A ZNetScene name found there is vanilla; a name that is not, is
/// a mod's.
///
/// The name of a prefab is the name of its .prefab file: that holds for 4806 of the 4807
/// vanilla prefab names Jotunn publishes (Valheim 1.0.7). The single exception - a vanilla
/// prefab whose file is named differently - is left out of the lists; a modded prefab is
/// never let in.
///
/// The set is built once, at the first "disguise list" or the first opening of the window
/// (reading 3.5 MB costs a moment, paid once per session and only when a list is asked for).
/// When neither file can be read - a stripped install, a future layout change - Contains
/// answers true for every name, so the lists keep exactly what they showed before, and one
/// warning says why.
/// </summary>
internal static class VanillaPrefabs
{
    /// <summary>The prefix of a location line in the manifest, before the asset path.</summary>
    private const string PathToken = "path in bundle: ";

    /// <summary>The extension that makes a located asset a prefab.</summary>
    private const string PrefabExtension = ".prefab";

    /// <summary>The vanilla prefab names, or null when no manifest could be read at all.</summary>
    private static HashSet<string>? s_names;

    /// <summary>
    /// False until the first Contains ran the load: "nothing readable" is a result too, and
    /// must not make every later call read 3.5 MB again.
    /// </summary>
    private static bool s_loaded;

    /// <summary>
    /// True when the name ships with the game - or when the manifests are unavailable, in
    /// which case nothing is filtered out and the lists stay complete.
    /// </summary>
    internal static bool Contains(string prefabName)
    {
        if (!s_loaded)
        {
            s_names = Load();
            s_loaded = true;
            if (s_names == null)
            {
                Disguise.LogWarning(
                    "Vanilla prefab manifests unavailable: the prefab lists are not filtered."
                );
            }
            else
            {
                Disguise.LogInfo($"Vanilla prefab set: {s_names.Count} name(s).");
            }
        }
        HashSet<string>? names = s_names;
        return names == null || names.Contains(prefabName);
    }

    /// <summary>
    /// Reads both manifests into one case-insensitive set, or null when not a single one of
    /// them yielded a prefab (no filtering is the safe answer to "I cannot tell").
    /// </summary>
    private static HashSet<string>? Load()
    {
        string dir = Path.Combine(Application.dataPath, "StreamingAssets", "SoftRef");
        string[] paths = new string[]
        {
            Path.Combine(dir, "manifest_extended"),
            Path.Combine(dir, "manifest"),
        };
        HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool readAny = false;
        for (int i = 0; i < paths.Length; i++)
        {
            if (ReadNames(paths[i], names))
            {
                readAny = true;
            }
        }
        return readAny && names.Count > 0 ? names : null;
    }

    /// <summary>
    /// Adds every "path in bundle: .../Something.prefab" of one manifest, under the file name
    /// without the extension. Returns false when the file is missing or fails part-way
    /// through: whatever it did read is kept, and a half-read set can only leave a vanilla
    /// name out of the lists, never let a modded one in - the other manifest is read next
    /// anyway.
    /// </summary>
    private static bool ReadNames(string path, HashSet<string> names)
    {
        if (!File.Exists(path))
        {
            return false;
        }
        try
        {
            foreach (string line in File.ReadLines(path))
            {
                int start = line.IndexOf(PathToken, StringComparison.Ordinal);
                if (start < 0)
                {
                    continue;
                }
                int first = start + PathToken.Length;
                if (!line.EndsWith(PrefabExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                int end = line.Length - PrefabExtension.Length;
                int slash = line.LastIndexOf('/');
                int begin = slash >= 0 && slash >= first ? slash + 1 : first;
                if (end > begin)
                {
                    names.Add(line.Substring(begin, end - begin));
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Disguise.LogWarning($"Vanilla prefab manifest unreadable ({path}): {ex.Message}");
            return false;
        }
    }
}
