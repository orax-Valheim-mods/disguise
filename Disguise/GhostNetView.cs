#nullable enable

namespace Disguise;

/// <summary>
/// The ZNetView an inert copy carries so its VisEquipment can run: the game's component with
/// BOTH of its Unity messages hidden - the type, without a single line of its behaviour.
/// The game's Awake would mint a real ZDO for the copy (the phantom networked object the
/// whole strip of GhostFactory exists to avoid) and its OnDestroy would walk references it
/// never held. Awake and OnDestroy are the only messages the component has, and Unity calls
/// a message on the MOST DERIVED class only, so these two never reach the game's code.
///
/// What VisEquipment needs from it is one single answer: GetZDO() -> null (m_zdo is never
/// set), which sends UpdateEquipmentVisuals down its own-fields branch - the copy's slots,
/// written by GearSkin - rather than a ZDO it can never have. Added only to the copies that
/// are SHOWN (BuildInert's keepEquipment), next to the VisEquipment it keeps alive: without
/// it, that component would throw on its null m_nview in Start and once per frame after.
/// </summary>
internal sealed class GhostNetView : ZNetView
{
    private new void Awake() { }

    private new void OnDestroy() { }
}
