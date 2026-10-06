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
| `disguise logs [on|off]` | Write the mod's own Info messages to the BepInEx log (warnings and errors are always written) |
| `disguise fly [on|off]` | Raise the creature above you for the others (visual only: your player stays on the ground) |
| `disguise gui [on|off]` | Open / close the Disguise window (`GuiKey`) |
| `disguise perf` | Frame time of the last few seconds: avg / min / max in ms and FPS (resets the window) |
| `disguise status` | Current state (creature, marker, save protection) |

## Configuration

File: `BepInEx\config\orax.Disguise.cfg`

| Option | Default | Role |
|---|---|---|
| `Enabled` | `true` | Disguise on/off |
| `CreaturePrefab` | `Troll` | Exact name of the creature prefab |
| `ProtectSaveFile` | `true` | Writes `Player` (not the creature) to the world save |
| `ReassertSeconds` | `2` | Re-application period on your player |
| `Logs` | `true` | Write the mod's own Info messages to the BepInEx log (warnings and errors are always written) |
| `ForceObserverRefresh` | `true` | Other players see a change right away (needs the DLL on the server) |
| `RefreshHoldSeconds` | `1.5` | Delay (seconds) before other players see the new look |
| `HideHelmet` | `true` | Hide the helmet from the other machines |
| `HideChest` | `true` | Hide the chest armour from the other machines |
| `HideLegs` | `true` | Hide the leggings from the other machines |
| `HideCape` | `true` | Hide the cape from the other machines |
| `HideHeldItems` | `false` | Hide the items in your hands and on your back |
| `ShowAsAlly` | `false` | Show the creature to the others with the green health bar of an ally instead of the red one of an enemy |
| `SeeYourDisguise` | `false` | Show the creature on your own screen instead of your viking (local only) |
| `FlyEnableKey` | `Home + LeftAlt` | Start the flight: the creature is raised for everyone who renders you |
| `FlyDisableKey` | `End + LeftAlt` | Land it: back on the ground |
| `FlyUpKey` | `UpArrow + LeftAlt` | Climb while held (once the flight is on), up to 100 m |
| `FlyDownKey` | `DownArrow + LeftAlt` | Descend while held, down to the ground |
| `FlySpeed` | `5` | Metres per second while a climb / descent key is held |
| `FlyScrollWheel` | `true` | Wheel raises / lowers the creature while the flight is on (the camera zoom waits) |
| `FlyScrollStep` | `0.2` | Metres per scroll notch; `0` gives the wheel back to the camera, a negative value reverses climb and descent |
| `GuiKey` | `F8 + LeftAlt` | Open / close the Disguise window (same as `disguise gui`) |
| `GuiScrollStep` | `3000` | Pixels the creature list moves per scroll notch; `0` holds the list still, a negative value inverts the wheel |

The four `Fly*Key` values are BepInEx **shortcuts** (`main key + modifiers`): in
ConfigurationManager (Alt+F10 by default) open `5 - Flight`, click the value and press the new
combination - no text to type. They default to `LeftAlt`, never `AltGr` (the RightAlt of a
French keyboard, which types characters), and the climb / descent keys only act while the
flight is on.

The **mouse wheel** climbs and descends too while the flight is on (`FlyScrollWheel`, one
notch = `FlyScrollStep` metres): the camera then waits, so the wheel never climbs *and* zooms
at once. The zoom comes back the moment the flight ends, and the minimap and the build
rotation keep their own wheel throughout.

The **Disguise window** (`GuiKey` = `F8 + LeftAlt`, or `disguise gui`) is built with **Jotunn**'s
Valheim-styled controls: the creature list, its 3D preview and a button for every command of
the table above (the branch adds them one step at a time - this step brings the **3D
preview**: the creature of the row the pointer is on is shown on the right of the window,
filmed by a camera of its own into a texture, and the configured one while the pointer is on
no row at all; the list already there is a filter field over a scrollable list of every
usable creature, the row of the creature configured right now in orange on beige rows, a
click on any row = `disguise set <prefab_name>` written and applied at once, and a wheel that
moves the list by `GuiScrollStep` pixels per notch, clamped at both ends so an extra notch
never makes it dip and spring back). The window is an **overlay**: nothing is paused,
you can still walk around with it open, and a frame belongs to the UI only while the pointer
is on the window **or** the filter field is focused - a click on a row never reaches your
character, the camera waits instead of spinning behind the window, and typing a filter never
walks your character away nor turns the camera.

The `Hide*` options fix gear displayed at nonsense positions on humanoid creatures: other
machines read the equipment hashes from your ZDO and attach them to the creature's model.
Your own view always keeps your equipment. The choice is published in **your** ZDO
(`cd_hide`): a dedicated server relaying your packets follows it, so your own config is the
only thing that matters - its `Hide*` defaults are just a fallback for a player whose client
has no DLL.

`ShowAsAlly` travels the same way (`cd_rel`): the other machines draw the green health bar
of an ally instead of the red one of an enemy, and only that display changes - never your
own view, the game rules or your equipment.

`SeeYourDisguise` (also `disguise view`) is the opposite for your own screen only: your
viking's renderers and cloth are switched off and a purely local, inert copy of the creature
takes its place - no ZDO, no collision, no AI, nothing sent to the server. It never changes
what the other machines see.

## Installation

Copy `Disguise.dll` into:

- `BepInEx\plugins\`

**Jotunn is required** (`BepInEx\plugins\ValheimModding-Jotunn\Jotunn.dll`, 2.30.2): the mod
cannot be loaded at all without it. Nothing is needed for the other players.

Full documentation (how it works, build & visibility audit, sources) lives in
`README.md` at the root of the repository.
