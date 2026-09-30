# Disguise

Makes **your player appear as a creature** to every other player - **including players who
have no mod installed**. You keep seeing and playing your own normal viking (unless you
switch on `SeeYourDisguise`, which draws the creature on your own screen too).

- **Only your PC is required.** Installing the DLL on the dedicated server is *optional but
  recommended*: it adds the automatic observer refresh and the save protection.
- No other player needs anything: their game is 100 % vanilla and still shows a creature in
  your place.

## Console commands (F5)

| Command | Effect |
|---|---|
| `disguise list` | List the usable creature prefabs (`disguise list troll` to filter) |
| `disguise set Troll` | Change creature and enable the disguise |
| `disguise enable` / `disguise disable` | Toggle the disguise |
| `disguise refresh` | Force every loaded player to re-create your object now (needs the DLL on the server) |
| `disguise view [on|off]` | Show the creature on your own screen instead of your viking (local only) |
| `disguise status` | Current state (creature, marker, save protection) |

## Configuration

File: `BepInEx\config\orax.Disguise.cfg`

| Option | Default | Role |
|---|---|---|
| `Enabled` | `true` | Disguise on/off |
| `CreaturePrefab` | `Troll` | Exact name of the creature prefab |
| `ProtectSaveFile` | `true` | Writes `Player` (not the creature) to the world save |
| `ReassertSeconds` | `2` | Re-application period on your player |
| `ForceObserverRefresh` | `true` | Other players see a change right away (needs the DLL on the server) |
| `RefreshHoldSeconds` | `1.5` | Delay (seconds) before other players see the new look |
| `HideHelmet` | `true` | Hide the helmet from the other machines |
| `HideChest` | `true` | Hide the chest armour from the other machines |
| `HideLegs` | `true` | Hide the leggings from the other machines |
| `HideCape` | `true` | Hide the cape from the other machines |
| `HideHeldItems` | `false` | Hide the items in your hands and on your back |
| `SeeYourDisguise` | `false` | Show the creature on your own screen instead of your viking (local only) |

The `Hide*` options fix gear displayed at nonsense positions on humanoid creatures: other
machines read the equipment hashes from your ZDO and attach them to the creature's model.
Your own view always keeps your equipment.

`SeeYourDisguise` (also `disguise view`) is the opposite for your own screen only: your
viking's renderers and cloth are switched off and a purely local, inert copy of the creature
takes its place - no ZDO, no collision, no AI, nothing sent to the server. It never changes
what the other machines see.

## Installation

Copy `Disguise.dll` into:

- `BepInEx\scripts\` - ScriptEngine (Gale game profile)
- `BepInEx\plugins\` - mod managers and the dedicated server

Full documentation (how it works, build & visibility audit, sources) lives in
`README.md` at the root of the repository.
