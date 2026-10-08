#nullable enable

namespace Disguise;

/// <summary>
/// The categories the prefab list is split into, one checkbox per kind in the window
/// (GuiCategories). Every prefab belongs to exactly one of them: PrefabCatalog.KindOf tests
/// them in this order and stops at the first match, so a piece that is also destructible
/// stays in Building and can never appear under two checkboxes.
///
/// Static is the first test of them all and the only one that is not about looks but about
/// MOVEMENT: a prefab without ZSyncTransform is drawn once, where its object was created,
/// and never moved again - ZSyncTransform is the one component in the game that writes a ZDO
/// position into a transform on a machine running no DLL of ours. What that means in game: a
/// wall, a tree or a rock stays where you changed your costume while a creature follows you
/// everywhere, and this category is what tells them apart before anyone picks one.
/// </summary>
internal enum PrefabKind
{
    /// <summary>
    /// No ZSyncTransform: walls, floors, furniture, trees, rocks - a world drawn once and
    /// never moved by a machine without this DLL. That machine is a vanilla client, and it is
    /// the very machine a costume is for (a modded one renders a real Player instead).
    /// </summary>
    Static = 0,

    /// <summary>A prefab carrying a Character: the list the mod shipped with, plus Player.</summary>
    Creature = 1,

    /// <summary>An ItemDrop: materials, weapons, food, trophies.</summary>
    Item = 2,

    /// <summary>
    /// A Piece carrying a ZSyncTransform. A Piece without one - every wall, floor and
    /// furniture - is claimed by Static before this test runs, so what is left here is the
    /// handful of buildable objects the game does move.
    /// </summary>
    Building = 3,

    /// <summary>
    /// The world itself: trees, plants, pickables, rocks and ore veins - again the moving
    /// ones only, the static majority of them being Static.
    /// </summary>
    Nature = 4,

    /// <summary>
    /// Whatever none of the five above claimed: spawners, markers, terrain objects - and any
    /// prefab the tests above no longer name, a ship being one of them since its category
    /// was dropped (a Piece is still Building: the raft and the karve never reach here). The
    /// list only ever drops a modded name or an effect one (PrefabCatalog).
    /// </summary>
    Other = 5,
}
