# Disguise

Makes **your player appear as a creature** - or as **any other prefab of the game**: an
object, a building piece, a ship, whatever `disguise list` prints - to every other player,
**including players who have no mod installed**.

- **Only your PC is required**; the dedicated server install is *optional but recommended*
  (§ 1.b - it adds the automatic refresh and the save protection).
- All other players stay 100 % vanilla yet see a creature in your place.
- You keep seeing (and playing) your own normal viking.

---

## 1. Installation

### a) On your PC (Gale game profile "dev")

Copy `Disguise.dll` into:

```
C:\Users\a\AppData\Roaming\com.kesomannen.gale\valheim\profiles\dev\BepInEx\scripts\
```

> ⚠️ **`Disguise.pdb` must be copied as well**, next to the DLL.
> ScriptEngine reads the assembly with Mono.Cecil using `ReadSymbols = true` and looks for
> `<name>.pdb` (extension replaced): without that file it throws
> `SymbolsNotFoundException: No symbol found for file ...` and **the plugin is never
> loaded** (it even blocks the loading of the scripts that follow it).
> The build deploys it automatically (see § 6).
>
> This folder is the one read by the **ScriptEngine** plugin (`com.bepis.bepinex.scriptengine`),
> loaded at startup - visible in the log:
> `[Info :Script Engine] Loading plugins from ...\BepInEx\scripts\Disguise.dll`.
> A reload (its `ReloadKey` chord) **unloads the running copy first** - `Destroy` on the object
> holding every script plugin, which calls its `OnDestroy` - and only then loads the DLL again
> into a new assembly. Everything this mod owns goes with that `OnDestroy` (§ 6, `Disguise.cs`):
> the window, the clone on your own screen, the pause menu's line and every Harmony patch, so a
> reload never leaves a second `Disguise` line in the menu.

> ⚠️ **Jotunn is a hard dependency of this mod**: it builds the window of `disguise gui`
> (§ 2) with Jotunn's Valheim-styled controls.
>
> ```
> ...\profiles\dev\BepInEx\plugins\ValheimModding-Jotunn\Jotunn.dll   (2.30.2)
> ```
>
> Already installed on the `dev` profile (Gale, library `ValheimModding-Jotunn`). Without
> it the DLL cannot be loaded at all: an error mentioning `Jotunn.dll` appears in the log
> and the `[Info : BepInEx] Loading [Disguise 0.1.0]` line never does (§ 7). **Nothing of
> it is needed by the other players.**

### b) On the dedicated server (r2modmanPlus profile "serveur") - *optional, recommended*

The mod works **with the DLL on your PC alone**: vanilla clients do see the creature, and
nothing else is needed for them to see it (verified with a client and a server that have
no mod). What the server DLL adds:

| Feature | With the DLL on the server | Without it |
|---|---|---|
| Vanilla clients see the creature | yes | **yes** (unchanged) |
| Automatic observer refresh after a change (§ 3, step 5) | **yes** (~`RefreshHoldSeconds`) | **no**: they keep your old look until they move ~100 m away from you, until a reconnect on either side - and `disguise refresh` does nothing |
| `ProtectSaveFile` (§ 2): the world save stores `Player` | yes | **no**: the server is what writes the save, so it stores the creature |
| Real `Player` object on the server (the `cd_disguise` marker patch) | yes | no: the server instantiates the creature prefab for your player |
| Fake map position (`disguise setFakePosition`, § 2) | **yes**: the server writes it into the player list it broadcasts | **no**: the other maps keep showing your real position |
| Flight seen by the others (`disguise fly`, § 2) | **yes**: the server raises the outgoing position only | **no**: only your own view flies, everyone else keeps you on the ground |

> ⚠️ Without the server DLL, run **`disguise disable` before stopping / saving the server**
> (§ 5): `ProtectSaveFile` does not exist there, so the creature would be written to the
> world save.

Copy the same DLL into:

```
C:\Users\a\AppData\Roaming\r2modmanPlus-local\Valheim\profiles\serveur\BepInEx\plugins\_manual_install\
```

This folder is created automatically by the build (see § 6).

The server needs **Jotunn** as well - same folder shape,
`...\BepInEx\plugins\ValheimModding-Jotunn\Jotunn.dll`, already installed on the `serveur`
profile (r2modmanPlus). The DLL is loaded whatever the window is used for, so without Jotunn
it fails to load there too (§ 1.a).

> ⚠️ **Exact locations (1 required + 1 optional, no more)**
>
> | Machine | Path | Loaded by |
> |---|---|---|
> | Your PC | `...\com.kesomannen.gale\valheim\profiles\dev\BepInEx\scripts\Disguise.dll` | ScriptEngine |
> | Dedicated server | `...\r2modmanPlus-local\Valheim\profiles\serveur\BepInEx\plugins\_manual_install\Disguise.dll` | BepInEx chainloader |
>
> The r2modman `Default` profile **must not** contain the DLL: the game is now launched
> through the Gale `dev` profile.

### c) Restart

1. Restart the **dedicated server** (only if you installed the DLL on it - § 1.b).
2. Restart **Valheim** through `Valheim (DEV).lnk` (Gale profile `dev`).
3. Check the logs for:
   `[Info : BepInEx] Loading [Disguise 0.1.0] (orax.Disguise)`
   - PC: `...\profiles\dev\BepInEx\LogOutput.log`
   - Server: `...\profiles\<server profile>\BepInEx\LogOutput.log`

**Do not** install the DLL for other players - and **nowhere else on your PC**
(the r2modman `Default` profile must no longer contain it).

---

## 2. Usage

In game, open the console with **F5**:

| Command | Effect |
|---|---|
| `disguise status` | Current state (prefab, marker, save protection) |
| `disguise list` | Every **vanilla** prefab you can become, with a count per category (`disguise list troll` to filter; the window shows the same names, § 2 GUI). Prefabs a mod adds, the effect prefabs (`fx_`, `sfx_`, `vfx_`) and a prefab with **no mesh to draw** are never listed (§ 6, § 7). The whole output goes to the BepInEx log too, while `Logs = true` (§ 7) |
| `disguise favorite [prefab]` | **Mark a prefab as a favorite**, or take the mark off - one command does either and says which (no argument = the configured prefab; the window's Favorite button runs it exactly that way, on the prefab selected in the window). The marks are kept in `6 - GUI` / `Favorites`, and the Favorites box of the window then lists them alone (§ 2 GUI) |
| `disguise set Troll` | Change the prefab and enable the disguise (a click on a row of the window does the same, at the level you are on, § 2 GUI). An optional **level** follows the name - `disguise set Troll 3` - and leaving it out brings the costume back to **level 1** |
| `disguise level 2` | The **level** the costume is drawn at, 1 to 9 - no argument reports the current one. 2 and 3 are the **starred** creatures: another skin where the prefab has one, a star over the costume, and the health of a real creature of that level (§ 2). The `-` / `+` row of the window runs this command |
| `disguise enable` | Enable the disguise |
| `disguise disable` | Disable: you become a viking visible to everyone again |
| `disguise refresh` | Force every loaded player to re-create your object **now** (manual fallback - needs the DLL on the server, § 1.b) |
| `disguise view [on|off]` | Show the creature on **your own screen** instead of your viking (toggles without an argument, purely local) |
| `disguise logs [on|off]` | Write the mod's own **Info** messages to the BepInEx log (toggles without an argument; warnings and errors are always written, § 2) |
| `disguise fly [on|off]` | **Raise the creature above you** for the other machines (toggles without an argument, § 2). Purely visual: your player never leaves the ground |
| `disguise offset <X> <Y> <Z>` | **Move the costume** that far from you - world units, `1` is one metre (no argument reports where it stands, `0 0 0` puts it back, § 3). Purely visual: your player never moves |
| `disguise rotation <X> <Y> <Z>` | **Turn the costume** in place, in degrees **on top of** the way it already faces (no argument reports it, `0 0 0` is the normal facing, § 3). Purely visual: your player never turns |
| `disguise setFakePosition [x z \| off]` | Show a **fake position** on the other players' map: no argument records where you stand, `off` shows the real one again (needs the DLL on the server, § 1.b) |
| `disguise gui [on|off]` | Open / close the Disguise window (`GuiKey`, § 2): same thing as the key chord and as the `Disguise` line of the pause menu, for whoever prefers the console |
| `disguise perf` | Frame time of the window since the previous call: avg / min / max in ms and FPS (the window resets at every report) |
| `disguise help` | List of the available commands (`disguise ?` also works) |

The console tab-completion suggests the sub-commands.

### Fake map position (`disguise setFakePosition`)

Valheim carries a player's position over **two unrelated channels**, and the map does not
use the ZDO one: `ZNet.SendPlayerList` broadcasts a player list fed by the reference
position each machine reports, and `Minimap.UpdatePlayerPins` draws the pin of another
player from it (`GetOtherPublicPlayers` has no other reader). The command fakes **that**
list - on the server, for the duration of the write:

- `disguise setFakePosition` records the position you are standing on: the others keep
  seeing you there while you actually move away;
- `disguise setFakePosition 10 20` uses that point of the map plane (X and Z; your current
  height is carried over - a map pin only draws X and Z);
- `disguise setFakePosition off` shows your real position again (`clear`, `none` and
  `disable` are accepted too);
- `disguise status` shows the published value.

Like `cd_hide` / `cd_rel` (**the owner decides**), the choice travels in your own ZDO
(`cd_fake` + `cd_fpos`, republished every `ReassertSeconds`, and disabled by writing an
explicit `0`, never by removing the key - § 3, step 5). The modded server then writes it
into the player list it broadcasts, and **only into that packet**: the ZDO position,
`peer.m_refPos` (what the server uses to decide which ZDOs a peer receives,
`ZDOMan.CreateSyncList`), the save file and **your own screen** all keep the real
position. Your character stays exactly where it really is - only the pin on the other
maps moves.

> ⚠️ The dedicated server must run this DLL (§ 1.b): without it nobody can write the fake
> into the player list, and the other maps simply keep your real position. It only
> replaces something while your position is shared at all - the *public position*
> checkbox of the enlarged map: with it off, no pin of you is drawn anywhere, fake or
> real. And it is session state: restarting **your** game drops it (the flag is published
> back as `0` on the next join), while `disguise setFakePosition off` clears it on the
> spot. It works whether or not the disguise itself is enabled.

### Configuration (`.cfg` file)

File: `...\profiles\<profile>\BepInEx\config\orax.Disguise.cfg`

| Option | Default | Role |
|---|---|---|
| `Enabled` | `true` | Disguise on/off |
| `CreaturePrefab` | `Greydwarf` | Exact name of the prefab you appear as: any **network** prefab of the game - a creature, an object, a piece, a ship (§ 3: a vanilla client rebuilds you from its own prefab table, so anything not registered there can never be shown; the two lists only show the vanilla, non-effect ones) |
| `CreatureLevel` | `1` | The **level** you appear as, 1 to 9 - the span the vanilla `spawn` command gives a creature: `Terminal` clamps its `[level]` argument with `Mathf.Min(level, 9)` **before** `SetLevel` (4 for an item), so `spawn Troll 1 100` hands out a level 9. The costume follows that same span wherever a level can be set (this entry, both commands, the window's `+`), the game itself never clamping. 2 and 3 are the **starred** creatures: another skin where the prefab carries one, a star over the costume, and the health of a real creature of that level (the bar the others draw is the costume's base health multiplied by it). Above the skins a prefab carries, only that health keeps growing. Written into your ZDO for every machine to read and saved with the config; change it with `disguise level <1-9>`, `disguise set <name> [level]` or the `-` / `+` row of the window (§ 2) |
| `ProtectSaveFile` | `true` | Writes `Player` (not the creature) to the world save - **needs the DLL on the server**, since the server writes the save (§ 1.b) |
| `ReassertSeconds` | `2` | Re-application period on your player |
| `Logs` | `false` | Write the mod's own **Info/Debug** messages to the BepInEx log - the whole output of `disguise list` included (§ 7). **Off by default**: a working install keeps its log quiet, and `disguise logs [on|off]` switches it (the command writes the value back, so the choice stays for the next start). Warnings and errors are always written (§ 7) |
| `ForceObserverRefresh` | `true` | Automatically make other players re-create your object after a change (§ 4.1) |
| `RefreshHoldSeconds` | `1.5` | Delay (seconds) before other players see the new look; increase on a laggy connection |
| `SeeYourDisguise` | `true` | Show the creature on **your own screen** (`4 - Own view`, local only) |
| `HideItemSparkle` | `false` | Hide the shimmer beside an **item** costume - the looping particle every dropped-item prefab carries on its root (`4 - Own view`, local only: your own costume and the window's picture, never what another machine draws; § 2) |

**Display section** (`3 - Display`) - what the **other machines** see while you are
disguised. `VisEquipment` reads the equipment hashes from your ZDO and
attaches those items to the bones of whatever prefab carries it: on a vanilla client that
prefab is your **creature**, so a helmet / cape / leggings gets glued at a nonsense
position on humanoid creatures. These options hide the concerned keys **for the other
machines only** - your own view always keeps your equipment.

| Option | Default | Role |
|---|---|---|
| `HideHelmet` | `true` | Hide the helmet from the other machines |
| `HideChest` | `true` | Hide the chest armour |
| `HideLegs` | `true` | Hide the leggings |
| `HideCape` | `true` | Hide the cape |
| `HideHeldItems` | `false` | Hide the items in your hands and on your back |
| `ShowAsAlly` | `false` | Draw the creature with the **green** health bar of an ally instead of the red one of an enemy |

How it works (`EquipmentMask`): the real hashes always stay in your ZDO, they are only
**swapped out of the packet** while `ZDO.Serialize` runs (the vanilla key carries `0`, the
real hash moves into our own `cd_eq_*` key). The world save reads a *snapshot* taken at the
start of the save (`ZDO.Save`), and the real hashes are written into that snapshot so the
file keeps your equipment; a Harmony patch on `VisEquipment.UpdateEquipmentVisuals` puts
them back for the modded machines, which therefore keep rendering your gear. Nothing is
ever persisted masked and no extra traffic is produced (the swap does not bump the ZDO
revision). `Hide*` changes are re-sent immediately while you are disguised.

**Who decides**: the choice travels with you. Your client writes the selected options as a
bitmask in **your own ZDO** (`cd_hide`, republished every `ReassertSeconds`), and every
machine relaying your packets masks according to *that* value. A dedicated server therefore
hides exactly what **you** asked for: its own `3 - Display` section is only the fallback for
a player whose client has no DLL at all (a disguise kept in the save, server alone), never
an override. Without this, the server's defaults (`Hide* = true`) would hide your gear on
every other client no matter what your config says.

**The health bar follows the same rule** (`ShowAsAlly`, `AllyView`): `EnemyHud` asks
`BaseAI.IsEnemy(the player looking, your ZDO)` whether to draw the red bar of an enemy or
the green one of an ally, and the only answer that function can read from your ZDO is the
tamed flag - an int, so an explicit `1` / `0` does reach a vanilla client. Your client
publishes the choice as `cd_rel`, and every machine serializing your ZDO writes it into
the packet and restores the previous value right after: the stored ZDO, the save file and
your own view never carry it. A modded machine also drops the flag on arrival, since it
renders a real `Player` whose bar is already the ally one - only a vanilla client, the one
drawing the creature, keeps it. Your relaying server follows `cd_rel` exactly like
`cd_hide`, its own `ShowAsAlly` being the fallback for an owner running no DLL.

These options can also be changed in game with `disguise enable` / `disable` / `set`.

**Own view section** (`4 - Own view`, `SeeYourDisguise`, `true` by default) - what **you**
see on your own screen. Your viking is substituted - its renderers and its cloth switched
off - by an inert local clone of the creature, so your screen renders one character: what a
vanilla client renders. That comes from your position, your rotation, and the animation
parameters read from your own ZDO (a creature animates mostly in its idle pose on a vanilla
client, because its sync list and the viking's only partly match - `GhostAnimation` reproduces
exactly that) **plus the triggers**, which travel as a bare RPC and would never reach a clone
that carries no `ZNetView` at all: `Patch_SetTrigger` hands them over, already resolved
against the creature's own controller (`CreatureTriggers`), so your own screen swings, jumps
and staggers with the creature like everyone else. Console command: `disguise view [on|off]`,
which switches it on the spot.

The other setting of this section is **`HideItemSparkle`** (`false` by default, the
`Hide item sparkle` box under the command buttons of the window). Every Item prefab of the
game carries a `ParticleSystem` **on its root** - the shimmer the vanilla game draws beside a
dropped item so it can be found - and a costume built from an item shimmers exactly like one
(while it is being filmed by the preview, and on your own screen with `SeeYourDisguise` on).
Ticking the box switches the **drawing** of every particle system of an Item costume off
(`GhostFactory.HideSparkles`: the renderer, not the object - that object is the costume
itself - and not the system either, which would start again at activation), so the costume
stands there without a shimmer while everything else it has is left alone: only an `Item` is
concerned, a creature, a piece or a ship keeps every effect it carries. Like everything of
`4 - Own view` it is **purely local**, and the reach of that is worth being precise about:
your own costume and the picture of the window are drawn by this machine, so they follow the
setting; a machine running no DLL of ours builds your **real item prefab** from the network
and has the sparkle baked into it, with no setting of ours inside to turn it off; and a
machine running the DLL draws no costume of yours at all (it builds a real `Player`, § 3).
A clone already standing rebuilds itself the moment the value moves - one bool watched per
frame, the expensive test made only when it can matter - and the box, the `.cfg` and
ConfigurationManager are the three ways to write it.

The clone is **purely local**: no ZDO, no collision, no AI, no network component.
`GhostFactory` builds it from a prefab deactivated for the duration of the instantiation,
which defers every `Awake` and lets the mod strip `ZNetView`, `Character`, `BaseAI`,
`ZSync*` and the colliders **before** the clone is activated (a live `ZNetView.Awake` would
create a phantom networked ZDO). The very same build is what makes the animation bridge able
to read a creature's trigger list at all (§ 3): the copy is activated, read, and destroyed in
the same call. The strip runs in **several passes**: Unity refuses to
remove a component another one still requires ("Can't remove Humanoid because CharacterDrop
depends on it"), and a single surviving `Humanoid` would wake up as a live `Character` - its
animator states call `IsTamed()` on it, `EnemyHud.TestShow()` throws on it from `LateUpdate`,
and its `OnDestroy` throws on the stripped `SEMan`, so the dead entry would stay in
`Character.s_characters` for the rest of the session: a health bar frozen on screen, an
exception per frame and the frame time to go with it. Anything non-visual still present
after the strip is therefore detected, and the clone is **discarded instead of activated**.
What the strip keeps on purpose is the visuals themselves - the transforms (the skeleton a
skinned mesh is bound to), `Animator`, `LODGroup`, the **mesh filters**, the renderers, the
particles and the lights: a `MeshRenderer` whose `MeshFilter` went with the strip keeps no
mesh at all and draws nothing, which is what used to leave every non-creature costume -
pieces, items, ships, carts and most static ones too - invisible on your own screen, while a
creature, drawn by a `SkinnedMeshRenderer`, never missed it at all. `LevelEffects` - the
component that dresses a creature for its **level** - is kept as well, by NAME (an `is` test
would be one more game type in the audit's count) and harmless without the `Character` above
it that the strip removes: `LevelSkin` then lends it the level, so a starred costume shows
its skin on your screen and in the window's picture too.
The clone sits at your player transform, copied once per
frame from `LateUpdate`, and is deliberately **not parented** to you: a child would be walked
by every `GetComponentsInChildren` the game and the mod run on your player, extending your
hierarchy by a whole creature. Your own camera, controls and equipment data are untouched -
nothing ever reaches the server or the other machines. Your visual components are re-scanned
ten times a second rather than every frame - nothing in the game re-enables one of them
(`Character.RPC_ResetCloth` only ever touches an already enabled cloth) - so the option costs
one transform copy per frame. The clone carries no equipment either: with
`HideHeldItems = false` the other machines still render your held item on the creature while
your own screen shows none, so enable that option too for a perfect match.

**Measuring what it costs** - `disguise perf` reports the frame time of the window since the
previous call (average / min / max in ms, plus FPS) and then resets it. Take three readings
of about five seconds in the same spot to separate this mod's own cost from the clone's:
`disguise view off` (your baseline), `disguise view on` with the disguise **off** (our code
running, no clone, nothing hidden), then `disguise view on` with `disguise set <creature>`
(the whole feature). The second reading should sit within noise of the first one - the
difference between the third and the second is what the clone costs on your screen, which is
exactly what the other machines pay to render you.

**Flight section** (`5 - Flight`) - making the **creature** rise, while your player never
leaves the ground (§ 3, and § 4.8 for what it means in game). The four keys ship
**unbound**: `FlyEnableKey` starts it (the creature is raised by 2 m straight away),
`FlyUpKey` / `FlyDownKey` climb and descend **while held**, at `FlySpeed` metres per second,
and `FlyDisableKey` lands it - while the console reaches the same two states at any time with
`disguise fly [on|off]`, which is all a fresh install needs before a shortcut is recorded
(the wheel, below, drives the climb and the descent too). `disguise status` reports the
current height.

The **mouse wheel** climbs and descends too while the flight is on (`FlyScrollWheel`, one
notch = `FlyScrollStep` metres), and the camera then waits: the wheel belongs to the creature
alone, so it never climbs *and* zooms at once - the zoom comes back the moment the flight
ends, and the minimap and the build rotation keep their own wheel throughout.

| Option | Default | Role |
|---|---|---|
| `FlyEnableKey` | *(none)* | Start the flight: the creature is raised for everyone who renders you |
| `FlyDisableKey` | *(none)* | Land it: back on the ground |
| `FlyUpKey` | *(none)* | Climb while held (only once the flight is on), up to 100 m |
| `FlyDownKey` | *(none)* | Descend while held, down to the ground and never below it |
| `FlySpeed` | `5` | Metres per second while a climb / descent key is held |
| `FlyScrollWheel` | `true` | Wheel raises / lowers the creature while the flight is on (the camera zoom waits) |
| `FlyScrollStep` | `0.2` | Metres per scroll notch; `0` gives the wheel back to the camera, a negative value reverses climb and descent |

The four keys are BepInEx **shortcuts** (`main key + modifiers`), and none of them has to be
typed: open **ConfigurationManager** (Alt+F10 by default), section `5 - Flight`, click the
value you want to change and press the new combination - it is recorded straight into
`BepInEx\config\orax.Disguise.cfg` (the `Clear` button unbinds it). Editing the file by hand
still works, as long as the format is kept (`Home + LeftAlt`, main key first), and either
source is read every frame, so a change applies immediately. A value written by an older
version (`LeftAlt+Home`, main key **last**) would be read reversed: re-record it, or swap the
two keys.

They ship **unbound**, which is a `KeyboardShortcut` with no main key at all: every chord
test answers false to one, so nothing here can fire by accident, and the console and the
wheel are enough before anything is recorded. A chord recorded by hand should keep away from
`AltGr`, which is the RightAlt of a French keyboard and types characters all day. Every key of
a shortcut must be held at once, in whatever order, and nothing else is required: the chords
are read directly instead of through
BepInEx's `KeyboardShortcut.IsDown`, which would refuse them while W, A, S or D is held. The
climb / descent keys only act **while the flight is on**, so a shortcut pressed by accident
never moves the creature.

**Nothing of it reaches your player.** The height is a value like any other published in
your own ZDO (`cd_fly`, in centimetres so that no float has to be written), and what your
body, the server, the AI, the combat and the save file work with stays exactly where it
was. Only two renderings change: your own clone (with `SeeYourDisguise`, § 2), and the
packet the server writes for everyone else (§ 3). Consequences worth knowing (§ 4.8): a mob
attacks the *ground* position the server simulates, your own attacks and landings are the
ones of a standing player, and **the others only see the flight while the DLL runs on the
server** (§ 1.b) - without it your own view still flies and everybody else keeps you on the
ground.

**GUI section** (`6 - GUI`) - the Disguise window, built with **Jotunn**'s Valheim-styled
controls (Jotunn is a hard dependency of this mod, § 1). Three things open it: `GuiKey`
(toggles), the console (`disguise gui [on|off]`, same thing), and a **`Disguise` line in the
pause menu** (`ShowMenuEntry`, `true` by default: off, the pause menu only holds the game's
own lines) - while `Escape` closes it like the Close button, the pause menu staying out of
that press (`Patch_MenuShow` blocks `Menu.Show` for that frame only, or the game would pause
on the same key) and the next Escape opening it as usual. That pause menu line is the game's
own menu drawn one line longer (`PauseMenuEntry`): the entry is a **clone of the Settings
button** - same look, same layout, same list, standing right after it - relabelled `Disguise`,
with a click that closes the pause menu through the game's own code (`Menu.Hide`, which also
unpauses the game and hands the cursor back) before the window opens exactly as the chord
opens it. The click the clone was born with is not cleared but **replaced**: a `Button` also
carries the listeners its prefab wired into it, which `RemoveAllListeners` leaves alone, so
the whole event goes and Settings cannot open behind the window the entry has just opened.
The option is read every time the pause menu opens and applied at once when it changes -
ConfigurationManager over the pause menu moves the line on the spot. The entry is built the
first time the menu opens and lives as long as that world's menu (one `Instantiate`, and a new
one after a scene change), and a line the option takes away is **destroyed** rather than
switched off: the gamepad's chain is rewound first, because Unity follows an explicit
`selectOnDown` whether the button at its other end still exists or not, so a D-pad sent to a
hidden entry would simply go nowhere. That chain runs through the entry after every open
(`Patch_MenuNavigation`), so a D-pad reaches it exactly where it stands. The title row carries the chord itself, between the title and
Close: `Open / close` over the current value of `GuiKey` (read from the config at every open,
`Not set` when it is empty), so the window always says how to call it back. The whole panel
is **draggable** (Jotunn's `DragWindowCntrl`: take hold of the wood anywhere that is not a
control and the window follows, never leaving the monitor), and **the place it sits is kept
in the config** - `WindowX` / `WindowY`, pixels away from the middle of the screen, `0` and
`0` by default, so a fresh install opens the window **centred** and one dragged somewhere
comes back there. The place is read every frame while the window is open and written back
once when it closes (a write costs the whole config file, so it never happens during a
drag), a window destroyed by a scene change keeping its place as well - there is no panel
left to read by then, which is why the last frame's is remembered. A place typed into the
`.cfg` by hand is honoured too, and kept on screen when it is read: a change of resolution
cannot push the window out of sight. The window shows
the line under the title, then the checkbox row under that line, the prefab list, the two pose rows under it, the 3D preview, the
command buttons, the level row and the sparkle box, the status line and, closing the window, the help line under it (the branch added them one step at a time): a row of five buttons
under the picture, each one running the sub-command its label stands for
exactly as the console would (`Add favorite` / `Remove favorite`, `Enable` / `Disable`,
`Refresh`, `Own view`, `Flight` =
`favorite <prefab>`, `disguise enable` / `disable`, `refresh`, `view`, `fly`), and reporting its answer on the
status line, the line above the help line that closes the window. The first button is a
toggle for the mark - "Add favorite" over a prefab with no mark, "Remove favorite" over one
that has - and it acts on the prefab
**selected** in the window, the one the state line names: the console's own
`disguise favorite` with no name, so the button and the command are one single call and the
answer it writes on the status line always names the prefab it changed. The second is a
toggle for the disguise itself: it reads "Disable" while the disguise is on and "Enable"
while it is off, and both that label and the line under the title are rewritten after every
click. Under those five
buttons sits a **band** with two controls on it. At the picture's own left edge sits the
**level row** - the word `Level`, then `-` and `+` around the number: the level the costume
is drawn at (§ 2, the `CreatureLevel` setting), each click running `disguise level <n>`
exactly like every other button of this window runs its sub-command, so the answer lands on
the status line below. The number is rewritten per frame, so a level typed into the console
or into ConfigurationManager lands without closing the window; and the two buttons stop
being clickable at the ends of the span (1 and 9) rather than running a command that could
not move the level. To its right sits the **sparkle box** - `Hide item sparkle`, the one
checkbox that is not in the row above, because it gates no list: it writes `HideItemSparkle`
(`4 - Own view`) and nothing else, the same control the row above is made of (box and label
are one hit target, `GuiCategories.BuildBox`); a click on it takes the shimmer off an item
costume at once, on your own screen and in the picture (both rebuild themselves, § 2), and a
value written in ConfigurationManager is pushed back into the box every frame like every
other label. The **line under the
title** reads
`Prefab: <name>   -   disguise <on|off>   -   Flight <on|off>   height: <y>   -   size <w> x <h> x <d> m` - the prefab
and the toggle from the config, the flight from `FlightControl`, the **height** that
flight lifts the costume by, in metres with one decimal: `0.0` while the flight is off
(`Disable` puts the height back to 0), the height it is clamped to (0..100) while it is on,
so the figure is 0 or more and never negative - a raise, not a position: the player and its
Y never move, only what the machines draw does - and the **size** of that prefab, in metres:
width, height and depth of one box around every mesh it really draws
(`PrefabResolver.CreatureSize`, which shares the preview's own rule - `CreaturePreview.DrawsMesh`
and `DrawnBounds`: particles and trails are effects the costume spawns rather than the costume,
a mesh on a GameObject the prefab leaves inactive is drawn by nothing, and a skinned mesh is
measured from its pose and not from the bind-pose box saved with the prefab), measured once on
the prefab asset and cached against its hash - never an instance, nothing is created to find
out. The box is taken where the machines will draw the costume, its root at the origin and its
scale still in it, rather than where the authoring scene parked the asset: `CookedEgg` stands
50 m up with a spare mesh 50 m below itself, and a box measured in the scene's units called
that egg 50 m tall. It is left
out of the line while the prefab has no mesh to measure. It reads `height` rather than `offset`
because the **Offset row below** is a different thing (the pose of the costume, described
just after the list): the line stays about the flight alone. The flight values are the two
that move on their own, so the line is brought up to date **every frame** the window is on
screen, written only when the words really changed. The status line
holds the hint about clicking a row while no
button has been pressed, and then the **first line** of the answer of the last one (it stays
until the next button, and the hint comes back with the next window): a command can print
several lines, the console keeps them all, and the first one is the headline - the notes a
command goes on printing after it stay in the console. Closing the window under that line is
the **help line**: it holds what the element the pointer is on is FOR - one sentence for the
five buttons and the sub-command each of them runs, for the seven boxes and the filter they
ask of the list, for the filter field and Clear, for the two pose rows and their six fields,
for the picture, the sparkle box, the state line, the chord, Close and the answer line
itself - and an idle line while the pointer rests on nothing that describes itself
(`Drag the panel to move the window - this line describes whatever the pointer rests on.`).
It is written by the element the pointer ENTERS and put back by the one it LEAVES, and the
words belong to the component that wrote them: a `Clear` is only obeyed from the component
the line belongs to, so the order Unity sends those two events in cannot leave a stale line
up, and the words are compared before they are written, so a pointer resting on one control
for a whole frame costs nothing. Each element carries its own `GuiHover` - one component
with Enter and Exit and no other interface, the same rule `GuiRowHover` was written with
(Unity's `EventTrigger`, which does this job with two entries, also implements scrolling,
dragging and the start of both, and an `EventTrigger` on a row swallowed the wheel and the
dragging of the list) - while the rows of the list need none of their own: they reuse
`GuiRowHover`, which already listens to the pointer for the preview. Being listened to is
also what makes an element hoverable at all: Jotunn switches raycasting off on every label,
so `GuiHelp.Watch` turns the object into a hit target, and that changes nothing else - an
event an object does not handle itself still walks up to the panel, which owns the drag, and
a Button keeps its own Enter and Exit (every component of one object is called). The picture
was the one element that was never a hit target; it is one now, for this and nothing else,
and not even while it has no texture (a hidden Graphic is not raycast), so its help arrives
with the picture itself. The panel is 670 px tall for these two lines: it grew DOWNWARDS by
the help line's 40, so nothing above the status line moved, and `GuiStatus.StatusY` grew by
the same 40 to keep that line's box at `-572..-616`. The **checkbox row**: seven
boxes in the band under the state line and over the list - `Favorites` first, then the six
categories in alphabetical order of their label, `Buildings`, `Creatures`, `Items`, `Nature`,
`Other`, `Static` - the last six one per
category, each hiding the rows of its category in the list below (the state is kept in
`6 - GUI` / `Show*`, so a choice survives a restart, and `disguise list` prints every category
whatever they say), and the first about the **marks** instead of a category: a favorite is
not a category of its own - the prefab keeps the one category it has - but a checkbox of its
own, and while `Favorites` is ticked the list shows the marked prefabs **alone**, the six
category boxes then being ignored (one click giving a favorites list, one more the categories
back - `ShowFavorites`, `Favorites` in `6 - GUI`). A mark is set by the last button of the
picture - on the **selected** prefab, the one the state line names, the very call
`disguise favorite` makes with no name - or by `disguise favorite [prefab]` for any other
one, and taken off by the same two; the window follows
the marks as they come and go, they being read from the config every frame while it is open,
and the list keeps its place while it does so - a mark is one row more or less behind the row
whose button was pressed, not another list to look at, where a category box (and a letter
typed in the filter) does send the eye back to the first row.
The rows of a category that is **off** are not built at the opening at all - an opening then
costs only what it is going to show - and are built the first time the box is switched on
(once, in the order of the list); a row is never destroyed, so switching a box off and back
on is only a `SetActive`. Ticking `Favorites` owes the rows of marks whose category is off,
and they are built the same way, once.
**`Static` is the only one of them about movement instead of looks**: a prefab **without**
`ZSyncTransform` - walls, floors, furniture, trees, rocks, most of what a world is made of -
is drawn by another machine once, where its object happened to be created, and never moved
again (§ 3), so a costume of that kind stays behind while you walk for anyone watching on a
vanilla client. This is why a wall has left `Buildings`: every buildable object that does
not move now lives under `Static`, where you can see what you are picking before it happens.
The
**prefab list**: a filter field with a **Clear** button at its right - it empties the field
and brings every row back, through the same `SetActive` re-run a checkbox asks for, then the
list returns to the top - over a scrollable list of every **vanilla** prefab (no
`fx_` / `sfx_` / `vfx_` effect either, and nothing a costume could not be **drawn** as: a
prefab with no mesh anywhere under it is left out too - `HasMesh`) - the exact
list `disguise list` prints, minus what the checkboxes have switched off (those rows are not
even built while their box is off, which is what keeps an opening to the cost of what it is
about to show) - and the marked prefabs alone while `Favorites` is ticked, whatever category
they are in - a **gold star** (`★`, `#FFD700`, written as a `<color>` tag inside the label's
own text, since one `Text` keeps a single colour for a whole label) on every row the marks
hold: put there by `Label` when a row is built and written again by `SyncFavoriteStars`
whenever the `Favorites` line changes (hidden rows too - a category can be switched on later
with no build in between) - the row of the
prefab configured right now highlighted in orange, and a click on any row hands out that
prefab **at the level you are on** (config written and applied at once, the line under the
title updated - and the picture above had already been showing that very creature at that
level, the hover having built it with the level of the moment). The rows read beige and the configured one orange - Jotunn paints the text of all
of its buttons orange, so the list itself is tinted beige for the difference to be visible -
and the wheel moves the list by `GuiScrollStep` pixels per notch (3000 by default = about 88
rows per notch; uGUI's own default is `1`). The list is **clamped
at both ends**: an extra notch at the top or the bottom simply does nothing, instead of the
elastic dip-and-spring-back Unity gives by default. And the place of the list **survives the
window**: closing it and reopening it finds the list exactly where it was, the same rows on
screen - the window is destroyed as a whole when it closes, so `GuiCreatureList` keeps the
offset of the content every frame and writes it back into the next one (memory only, like
the preview's hover; a place the list has grown out of meanwhile is clamped inside the rows
it has now). The **3D preview** above the buttons
shows the last creature the pointer was on, and the configured one before any row has been
pointed at in this opening: leaving the list does not take the picture back - only another
row does - so a row can be looked at and then acted on with the same creature on screen the
whole way.

Directly under the list sit the **two rows that pose the costume**: `Offset (m)` with its
three fields and `Rotation (deg)` with three more, in the order X, Y, Z - all six editable,
and a change applies **as you type** (`GuiPose`, the window's side of `PoseControl`). The
offset is in world units (`1` is one metre, the same unit as the height on the line above),
the rotation in degrees **on top of** the way the costume already faces, so `0 0 0` is the
game's own pose and nothing changes. The console takes the same two values as
`disguise offset <X> <Y> <Z>` and `disguise rotation <X> <Y> <Z>` (§ 3), and both sides go
through the same parser, so `1.5` and `1,5` land alike; a box that is not a number yet -
empty, a lone `-`, half a typed number - simply keeps the value it had, and a value set from
the console while the window is open lands in the fields, never in the one being typed in.
They are **memory only** like the flight height, so they start at `0 0 0` on every launch,
and what you type reaches your ZDO within a tenth of a second (§ 3). Typing in them hands the
frame to the UI exactly as the filter field does: the character waits instead of walking off,
the camera waits instead of spinning behind the window.

The **3D preview** (`CreaturePreview`) is not a scene but a texture. On the first creature
asked for, the stage is built once: a free layer (the highest one no mod named, 31 down to
8, else one of the three Unity keeps spare), a camera of its own (`enabled = false`,
`cullingMask` that layer alone, a transparent clear colour) rendering into a `RenderTexture`
as big as the picture (380 x 300 - the canvas under the window is 1 unit = 1 screen pixel,
so nothing is ever scaled), and two directional lights on that layer, the key one hung on the
camera so the side you look at is always lit. The stage stands **20 km under the world**, and
every camera and every directional light of the game has that layer taken away while the
stage exists: the cameras would draw the creature into the world, and the lights would light
the preview with the moment's sun - a picture that turns black at night. The creature itself
is `GhostFactory.BuildInert`, the inert copy `SeeYourDisguise` already uses (§ 2): no ZDO, no
AI, no collider, no sound. Its skinned meshes are told to `updateWhenOffscreen` (one outside
every camera stops being deformed, and the picture would freeze), its animator is evaluated
**once and switched off** - the pose is the first frame of its own default state - and the
camera turns around the creature at 25 degrees per second instead of the creature turning, so
the framing measured once from the bounds stays true. A skinned mesh is measured from its
**pose** and not from `Renderer.bounds`, which hands out the bounds saved with the prefab -
and those are the bounds of the **bind pose**, the arms-out stand the model was authored in.
`Abomination_ragdoll` lies folded over half of what its saved box says (14 x 16 x 13 m saved
against 7.5 x 6.7 x 7.9 m of skeleton), so the camera was framed twice too far back and the
corpse filled a corner of its own picture; the living Abomination framed too large for the
same reason (12 m saved, 7.5 m of skeleton). The pose is settled before the measure and
nothing moves it again (the strip takes `Cloth` and every script, the animator is off), so
the box baked at that moment is the box of every picture the turn takes. That framing fits
the box of the meshes
along **both axes** and not a sphere around them, which is what fills the picture: the camera
goes round the object, so what it can meet at its widest is the plan diagonal (the widest a
box ever presents while turning) against the horizontal field of view, and what it meets at
its tallest is the height - which the turn never changes, and only the tilt adds a share of
the plan to. Both are exact worst cases over the whole turn, so nothing is ever clipped; a
sphere would have fitted the longest corner to corner line and left every other direction
empty, a ship filling the picture end to end showing a third of its height, an Acorn a fifth
of it. The camera never stands closer than
**40 m** to what it films: the stage is 20 km from the origin, where a float keeps about two
millimetres, and a camera a hand's width from a small prefab was carried over whole pixels of
rounding with every step of the turntable - an Acorn or an AxeHead trembled in the picture
while a creature, metres farther off, did not. What the framing would put nearer is **grown
on the stage** instead (the picture does not change: a projection cannot tell a bigger object
from a receded camera, and the box grows with the clone), and the rounding falls to a
twentieth of a pixel. Each film holds the world's fog and
ambient off the stage and restores them before Unity renders anything else (cameras run after
every `Update`, and `DisguiseGui.Tick` is one). A hover is held back 0.15 s before its copy
is built: a pointer sweeping the list would otherwise ask for one inert copy - up to ten strip
passes over every component of it - per row crossed.

The window is an **overlay**: nothing is paused, and you can still walk around with it open.
The frames that belong to the UI are the ones where the pointer sits on the window **or**
where the filter field is being typed in, which `DisguiseGui.Tick` measures once per frame
(`DisguiseGui.BlocksInput`, read by `Patch_TakeInput` and `Patch_SetMouseLook`): a click on
a row never reaches the character, the camera waits instead of spinning behind the window,
and typing a filter never walks the character away nor turns the camera - Valheim does the
same with `Hud.m_buildUi.SearchFieldFocused` for the build menu search. Pointer anywhere
else and no filter focused, both patches stay silent and the game runs as usual. While the
window is open, Jotunn's `GUIManager.BlockInput` takes the
mouse away from the camera (`GameCamera.m_mouseCapture`, without it
`GameCamera.UpdateMouseCapture` would lock the cursor again on the next frame) and gives it
back on close - including after a scene change, which is why `DisguiseGui.Tick` checks the
window every frame: a window destroyed by a scene change cannot release Jotunn's request
count on its own, and one exit to the menu would leave the cursor free in every later world.

| Option | Default | Role |
|---|---|---|
| `GuiKey` | *(none)* | Open / close the window (a BepInEx shortcut like the flight keys, recorded in ConfigurationManager, section `6 - GUI`, and shown on the title row of the window itself - **unbound** by default, the pause menu's `Disguise` line and `disguise gui` opening it instead). `Escape` also closes it, and the pause menu waits for the next press - whose own list carries a `Disguise` line opening it too (§ 2) |
| `GuiScrollStep` | `3000` | Pixels the prefab list moves per mouse wheel notch (section `6 - GUI`). Read every frame while the window is open, so a change takes effect at once; `0` holds the list still and a negative value inverts the wheel. uGUI defaults to `1` - one pixel per notch, which is what makes an unconfigured list crawl |
| `ShowMenuEntry` | `true` | Show the `Disguise` line of the **pause menu** (§ 2), standing right under the game's own Settings line and opening the window - the way a player who has bound no `GuiKey` reaches it. Off, the pause menu holds only the game's own lines. Read when the menu opens, and applied at once when it changes |
| `WindowX` | `0` | Where the window sits, **left to right**: pixels away from the CENTRE of the screen (section `6 - GUI`). `0` = centred, and it is the default - a window nobody has dragged opens in the middle. Written back when the window closes (a drag writes it, and the place read every frame is what goes in), put on the panel when it opens, and kept on screen when read, so a hand-typed value or a change of resolution cannot leave the window outside the monitor |
| `WindowY` | `0` | Where the window sits, **bottom to top**: pixels ABOVE the centre of the screen, a negative value putting it lower. `0` = centred (the default); with `WindowX` it is the whole place of the window, remembered from one session to the next exactly like it |
| `ShowStatic` | `false` | Show the **static** prefabs in the window's list: every vanilla prefab **without** `ZSyncTransform` (walls, floors, furniture, trees, rocks). A machine with no DLL of ours draws them once where their object was created and never moves them again, so a costume of that kind stays where you changed it for a vanilla client (§ 3) |
| `ShowCreatures` | `true` | Show the creature prefabs (a `Character`) in the window's list (section `6 - GUI`). One checkbox per category, one category per prefab: a category switched off only hides rows in the window, and `disguise list` keeps printing every vanilla prefab of every category |
| `ShowItems` | `false` | Show the dropped-item prefabs (an `ItemDrop`: materials, weapons, food, trophies) in the window's list |
| `ShowBuildings` | `false` | Show the buildable prefabs that **do** move (a `Piece` carrying `ZSyncTransform`: a cart, a raft, a training dummy). Walls, floors and furniture have none of it and are listed under `ShowStatic` |
| `ShowNature` | `false` | Show the natural prefabs that do move (trees, plants, pickables, rocks, ore veins - the static majority of them is under `ShowStatic`) |
| `ShowOther` | `false` | Show the prefabs no other category claims (spawners, markers, terrain objects) in the window's list. Off, the list only holds what the five other checkboxes show - nothing is ever dropped from the catalogue itself |
| `ShowFavorites` | `false` | Tick the window's **Favorites** box: the list then shows **only** the prefabs marked in `Favorites`, and the six category boxes are ignored while it is ticked (untick it to be back on the categories). One click gets a favorites list, one more gets the categories back; a row hidden this way is hidden in the window only, and `disguise list` keeps printing every prefab of every category |
| `Favorites` | *(empty)* | Comma-separated names of the prefabs marked as favorites (e.g. `Troll,Draugr,Wolf`), written by the window's Favorite button or by `disguise favorite [prefab]`, read by its Favorites box. The names are the ones `disguise list` prints; a name no catalogue holds simply never matches a row, and nothing here is ever dropped from the catalogue itself |

---

## 3. How it works (technical summary)

In Valheim, every network object (ZDO) carries the **hash of its prefab**
(`ZDO.m_prefab`), serialized both in the network traffic (`ZDO.Serialize`) and in the
save file (`ZDO.Save`). The chosen prefab determines **what each client instantiates**.

1. You are the **owner** of your player ZDO. The mod therefore changes its prefab to
   `Troll` (or another): `ZDO.SetPrefab` → `IncreaseDataRevision` →
   `ZDOMan.ClientChanged` → the server receives the data
   (`ZDOMan.RPC_ZDOData` → `ZDO.Deserialize`) and **relays it to every other client**,
   modded or not.
2. A vanilla client thus receives `prefab = Troll` and **instantiates a Troll** in your
   place. It does not know it is a player: no mod required on its side. That creature then
   reads its **level** out of the same ZDO - the game's own `level` key, `1` when it is
   absent - and dresses itself for it: the skin of a starred creature at levels 2 and 3
   where the prefab carries one, a star over the costume, and its health multiplied by
   the level. `CreatureLevel` (§ 2) is therefore written into your ZDO next to the
   marker, on your own ZDO only - and it can never reach the viking underneath, since the
   game reads that key for every entity that is **not** a player (`Character.Awake`).
3. Modded machines (your PC, plus the server when the DLL is installed there) intercept
   `ZNetScene.CreateObject` and, thanks
   to the marker stored in the ZDO (`cd_disguise`), **still instantiate the real `Player`
   prefab**, so that all server-side game logic (health, inventory, interactions, combat)
   stays strictly normal.
4. On your side, your `Player` object is created by `Game.SpawnPlayer`
   (`Object.Instantiate`) and never by `CreateObject`: **you keep exactly your usual
   view and character.**
5. **Observer refresh** (a vanilla client that already instantiated you keeps that
   GameObject forever - `ZDO.Deserialize` only overwrites `m_prefab`, and vanilla only
   re-creates objects that leave and re-enter their active area). So, right after a
   change, the mod raises a transient flag on your ZDO (`cd_refresh`) and the modded
   server relays that ZDO with a **fake far-away position** for `RefreshHoldSeconds`:
   - every vanilla client's `RemoveObjects` destroys your instance (your ZDO is now
     outside their active area);
   - when the flag is cleared, the real position comes back and the next
     `CreateObjectsSorted` tick re-creates your object **with the current prefab**.
   - the server's own ZDO position is never modified (only the outgoing packet is, and
     only inside `ZDOMan.SendZDOs`), so saves, game logic and your own connection are
     untouched; your own client can never apply that fake position anyway, because a ZDO
     update is only accepted when its `DataRevision` is strictly newer than yours - and
     you author every revision of your own player ZDO.
   - **the flag is cleared by writing an explicit `0`, never by removing the key.**
     `ZDO.Deserialize` only adds/overwrites the keys present in the packet
     (`ZDOExtraData.Add` never deletes): a removed key would simply be absent from the
     payload, the server would keep `cd_refresh = 1` forever, relay the fake position
     permanently and **make you invisible** to vanilla clients (their instance is
     destroyed and never re-created). Same rule for the `cd_disguise` marker.
   The result: other players see the new look **~1.5 s** after `disguise set/enable/
   disable`, and after a **level** change as well - a creature reads its level once, when
   it is born (`Character.Awake`), so a machine already drawing it needs a fresh object
   before a new skin can show - without moving and without anyone reconnecting.
   > This step runs **entirely on the server** (`ZDOMan.SendZDOs`): it needs the DLL
   > installed there (§ 1.b). Without it, everything else still works.

Visual synchronisation on vanilla clients:

- **Position**: yes, but **only for a prefab that carries `ZSyncTransform`** - the one
  component in the game that writes a ZDO position into a transform on a machine running no
  DLL of ours (a vanilla client, the machine a costume is for). Creatures, players, ships,
  carts and dropped items have it: they follow you everywhere. A prefab without it - every
  `Piece` (wall, floor, furniture), most of the nature - is instantiated at the position your
  ZDO had **at that moment** and never updated again: on a vanilla client a building costume
  stays where you changed it, and so does every `Failed to find rpc method` the machine logs
  about it (§ 7). The `Static` category of the window and the `static` count of
  `disguise list` are exactly that set of prefabs, so the list tells you what a costume will
  do before you pick it.
  The position that reaches the other machines comes from that same header, and the value
  behind it is the owner's **rigidbody** position: a few millimetres of physics jitter
  (penetration recovery, contacts), changing at every tick even with the character standing
  still. `ZSyncTransform.SyncPosition` chases that moving target with a `0.2` lerp on every
  DataRevision, and a costume - a rigid mesh with no animation to hide it behind - shows the
  chase as a permanent shiver of a few millimetres. The relaying server therefore hands back
  the **last position it sent** while the real one stays within **1 cm** of it
  (`PositionHold`, the same getter and the same packet-only window as the flight raise): the
  target freezes, the receiving object lerps to it once and then stops moving at all, and a
  character that really walks away leaves the band in a single packet. Nothing is written
  into a ZDO - the owner's ZDO, the server's state, the saves and the game logic keep the
  real position - and the observer refresh is excluded, its far-away position being the one
  thing that has to travel.
  Held still is not the whole story, though: a costume standing where you stand can still
  **dig into the ground**. The receiving body has gravity and velocity turned off and is
  dragged back to the relayed position with a `0.2` lerp every frame, so whatever pushes it
  out of the ground comes straight back - a raft's hull and a cart's wheels hang below your
  feet, and the costume wears **your upright rotation**: on a slope a raft stays horizontal
  while its uphill edge is buried. The relaying server therefore raises that same position
  to where the costume actually **rests**, and lays it on the ground it rests on
  (`RestOffset`): everything its colliders carry below the level your feet stand on
  (measured once per prefab, and only over what the machines really **simulate**: a collider
  on an object the prefab leaves inactive touches nothing, so it is not the ground it stands
  on - `CookedEgg` and `MushroomYellow` each ship such a spare with a collider fifty metres
  below themselves, which asked for the lift of a costume a metre deep and left both
  floating - the meshes are read only to be logged beside them), plus the
  part of the ground under that footprint which the plane fitted over it cannot explain,
  plus the clearance that keeps it from touching at all. Two different asks of the ground:
  the **tilt** follows the fitted plane only while the terrain is what the costume rests on
  (a floor under it, water and thin air are all far below, a roof overhead is far above, and
  none of them has a slope to follow), while the **rise** only has to see the terrain rise
  above your feet - it can raise a costume and never lower one, and that is precisely what
  buries one on a hillside while you stand a plank higher than the terrain. The ground is
  never guessed at either: the game's own read is a ray that answers with the height it was
  given when it hits nothing, so a missing collider and a perfectly level slope are one and
  the same number. The ray is therefore cast again by hand (hit and miss both known) over
  the terrain layer, with **three more reads behind it**: the same ray through every layer
  keeping only what carries a `Heightmap` - terrain recognised by what it is rather than by
  a layer name - the loaded heightmap, which needs no collider at all, and, where this
  machine has no terrain instance standing under the costume at all, the zone built for
  that read. That last case is not hypothetical: the game keeps real zones - heightmap,
  mesh, collider - only around its own reference position, which on a dedicated server is
  never moved to a player, and the zones a player walks in are generated in *ghost* mode,
  world data whose temporary root is destroyed the moment it exists (`ZoneSystem.Update`
  calls `CreateGhostZones` when `CreateLocalZones` did nothing). The game itself has no
  ground to read there, so `GroundProbe` builds the missing zone exactly as `SpawnZone`
  would - the game's own zone prefab at the zone position, behind the game's own
  `IsTerrainReady`, which is also what queues the terrain build - and, when that build is
  still running, waits for it (bounded, one wait open at a time) instead of letting the
  packet leave without its ground: the terrain data a tile is built from is *consumed* by
  that build, and the server has already spent this zone's data once - the ghost pass built
  the tile, instantiated it and destroyed it - so the first read here has to rebuild it
  before anything can stand on it, and the game sends a costume again only when the costume
  itself changes, which turns one packet without its ground into one *wrong minute*. It
  then reads the heightmap the root carries, and drops the root again after ten seconds
  without a read. Nothing is ever registered in `ZoneSystem.m_zones`: the game never learns
  the zone is there, its lifetime never touches it, and from the moment it exists the three
  reads before it find it like any other terrain. The report names whichever answered
  (`ray`, `probe`, `heightmap` or `zone`), and a costume that still has nothing under it
  prints a self-test line of its own with the position, the terrain layer and its mask, what
  the unfiltered ray meets on the way down, how many terrain maps this machine has
  registered, and why the zone could not be built (`Ground rest: no ground under <name> at
  (x, y, z) | terrain mask <n> (layer <n>) | ... | zone probe <reason>`) - so
  `no terrain read` says *why* instead of only *that*. The first zone built this way is
  announced once (`Ground rest: no terrain instance stands where the costume is read, so the
  zone at (x, z) is built to read it.`). What cannot fight back is never measured:
  no `ZSyncTransform` (nothing pulls it back to the ZDO), a kinematic rigidbody (moved with
  `MovePosition`, which no contact pushes back), or a `Character` of its own (a creature
  settles itself - the reason a `Troll` costume never shivered). Same rules as the band:
  server only, outgoing packet only, handed back as a copy, observer refresh skipped, and
  the very same lift added on your own screen's clone (`SelfView`) so both views of a
  costume agree to the centimetre. With `Logs = Enabled` the log writes the measurement once
  (`Ground rest: <name> hangs <n> m below its colliders, ...`), then one line per change with
  the lift, the tilt, the height read under your feet and the source it came from, and the
  rise (`Ground rest: <name> is relayed <n> m above its player, laid <d> degrees off upright
  | ground <x> m vs feet (<ray|probe|heightmap|zone>), rise <r> m`), and - since a tilt moves the
  lift by less than
  the two centimetres that line used to be waited for - a line of its own when the tilt is
  really written into the packet, which is what separates "the ground was flat" from "the
  tilt never left this machine".
- **Costume health** (`HealthMask`): a machine running no DLL of ours reads the health of
  your costume out of **your** ZDO - `WearNTear` asks for `health` with a fallback on the
  prefab's own maximum - so a piece of furniture wearing you shows 100 hit points against
  its 500: broken planks on a cart, a torn sail on a raft, where the clone your own screen
  draws is brand new (a creature reads the same key as a `Humanoid`, and gets your bar at
  the creature's scale). For the duration of `ZDO.Serialize` the relaying server writes the
  costume's own maximum into that key and puts yours back the moment the packet exists:
  the swap does not bump the DataRevision, never touches this machine's own state, and
  `ZDO.Save` - the path the world file reads - does not go through `Serialize` at all.
  **Only** the relaying machine swaps: an owner serializing its own ZDO would push the
  costume's maximum at the server, and `ZDO.Deserialize` stores every key a packet carries,
  so your real hit points would be replaced for good. Everyone else - vanilla and modded
  clients alike, there being no per-peer packet - is then shown a whole costume, and nobody
  but you and the server can tell how hurt you are through a disguise any more. The
  maximum is measured once per prefab from `WearNTear.m_health` / `Humanoid.m_health`, read
  by NAME and through reflection so it stays out of the audit's count; a prefab carrying
  neither (a dropped item, which shows no damage anyway) simply keeps your value.
- **Flight** (`cd_fly`, `FlightControl`): your own client writes the requested height into
  your ZDO - in **centimetres**, because `ZDO.Set` has no float overload in the original
  assembly - and that write is throttled to one per 100 ms so a held key cannot bump the
  DataRevision every frame. Your body itself never moves: the position the server holds, the
  AI and the combat run on, and the save file writes is the grounded one. A position travels
  on the wire in exactly **one** place: the per-ZDO header `ZDOMan.SendZDOs` writes through
  `ZDO.GetPosition`, which the receiver applies with `ZDO.InternalSetPosition` *before* it
  deserializes the rest of the ZDO - `ZDO.Deserialize` never reads a position at all (the
  only other writer of one, `ZDO.Save`, is the save file; `ZDO.Serialize`, the body, carries
  keys only). The raise is therefore a postfix on that getter, open only while
  `ZDOMan.SendZDOs` is building a packet (`Patch_ZDOGetPosition`, window opened and closed
  by `Patch_SendZDOs`): it hands back `y + cd_fly` and **writes nothing**, so the packet
  header is raised while the stored ZDO is not - no fake position anywhere, nothing to
  restore, no window that could be left open. Two gates keep it honest: it runs **only when
  that machine is the server** (a client sending a raised position would have the server
  store it), and **only while your prefab really is the creature**, so a viking is never
  drawn in the air. The same getter also sorts a packet that is full by distance
  (`ServerSortSendZDOS`), so a flying player can sort up to 100 m farther than it is and
  land in the next packet - never in a different position. Without the DLL on the server no
  machine relays the raise and everybody but you stays on the ground (§ 1.b); on your own
  screen the clone simply gets your height added to your transform once per frame
  (`SelfView`), and a machine running this DLL - which renders you as a real `Player` - sees
  that object fly, since it follows the very same ZDO position.
  The **wheel** climbs and descends alongside the two keys: `FlightControl.Tick` reads it
  once per frame (Unity reports one detent as `1`, times `FlyScrollStep` metres), and while
  the flight owns it the scroll `GameCamera.UpdateCamera` reads inside its own call is handed
  back as `0` (`Patch_UpdateCamera` opens that window, `Patch_GetMouseScrollWheel` makes the
  read worth nothing). That cancels exactly the zoom of the frame, because the method writes
  only `GameCamera.m_distance` with that scroll - and only it: the minimap, the build
  rotation and the climb itself all read the wheel outside the window.
- **Pose** (`cd_ox` / `cd_oy` / `cd_oz` and `cd_rx` / `cd_ry` / `cd_rz`, `PoseControl`): the
  offset and the rotation of the costume, set from `disguise offset` / `disguise rotation`
  and from the two rows of the window, and **memory only** - they start at zero on every
  launch, and the publication below writes that zero back into the ZDO, which is what erases
  a value an earlier session left there. Your own client writes the six values into your ZDO
  as **ints** (the offset in centimetres, the rotation in centidegrees: `ZDO.Set` has no
  float overload), one pass per 100 ms at most and only for a value that really changed, so
  typing never floods the DataRevision. The offset then rides the very same channel as the
  height - a postfix on `ZDO.GetPosition`, open only while `ZDOMan.SendZDOs` builds a packet
  on the **server** - which adds `cd_ox / 100`, `cd_oy / 100` and `cd_oz / 100` to the copy
  going into the packet header, *after* the ground rest has measured the spot and *before*
  the flight raises it, and writes nothing. The rotation cannot ride that header (a header
  carries a position, never a rotation): `ZDO.Serialize` writes `m_rotation` into the body
  whenever it differs from identity, so the server composes
  `current * Euler(cd_rx, cd_ry, cd_rz)` into that one value for the duration of the body
  being built and puts the stored rotation back immediately after (`Patch_ZDOSerialize`
  opens `RestOffset`'s own lay-on-the-ground tilt **first** and this swap **after** it, and
  closes them the other way round, so a single rotation - ground first, turn on top - leaves
  the server; the other order would erase the turn, because a lay-on-the-ground is a
  `FromToRotation` onto the ground normal: it pins the up axis of whatever it lands on, so a
  tilt composed on top of the turn would take its pitch and its roll back out and leave only
  the yaw. The receivers apply it where they build the object and where `ZSyncTransform`
  syncs it). Neither swap bumps the
  DataRevision, neither reaches `ZDO.Save` - the world file reads through `ZDO.Save`, never
  through `Serialize` - and both are the relaying machine's alone, because an owner rewriting
  its own rotation would have the server STORE it. Same two gates as the flight, server only
  and only while your prefab really is the creature, and the same consequence without the DLL
  on the server (§ 1.b): your own screen moves and turns the clone at once (`SelfView` hands
  the ground rest your own rotation and composes the turn **on top of** the result, the same
  order the packet leaves the server in), while everybody else keeps it where it was.
- **Animations**: partial, and bridged for melee attacks and for the block key. `ZSyncAnimation.SetTrigger`
  carries no name at all inside the ZDO: it broadcasts the raw **parameter name**, and each
  machine applies it to the animator of the object *it* built for that ZDO - a real `Player`
  here, the creature on a vanilla machine. A name the creature's controller does not have is
  dropped without a trace (`ZSyncAnimation.Awake` sets `Animator.logWarnings = false`),
  which is why a goblin never played `swing_axe0`, `knife_stab1` or `bow_fire`: `goblin_animator`
  only knows `jump`, `spear_throw`, `stagger`, `swing_longsword` and `wakeup`.
  `Patch_SetTrigger` therefore sends such a melee attack a **second time**, under a name the
  creature really has (picked by `CreatureTriggers`, e.g. `swing_longsword` for a goblin).
  That name is now the creature's own attack for the button you used: its **normal** one for a
  normal swing, its **special** one for a secondary attack (middle click by default) - the two
  designated by `CreatureAttacks` from the creature's weapon pool (`Humanoid`:
  `m_defaultItems` / `m_randomWeapon` / `m_randomItems` / `m_randomSets` /
  `m_unarmedWeapon`, i.e. exactly the
  items `MonsterAI.EquipBestWeapon` equips from), because no creature item carries a
  secondary attack the game could point at: the plain melee with the **shortest**
  `m_aiAttackInterval` is the normal one, the **longest** one is the special (a pool holding
  a single attack has no special: both attack keys then play the normal one, as a boar or a wolf
  must). Measured: Troll `punch` (4 s) vs `attack` ground slam (20 s), Goblin `swing_longsword`
  (2 s) vs `spear_throw` (4 s), Greydwarf `attack` (2 s) vs `throw` (8 s). Only names the
  creature's own controller really has are ever used; when no pool can be read (no `Humanoid`,
  empty pool) it falls back to the original word match.

  The third combat key - **block**, right click by default - has no trigger to bridge at all:
  the game travels a block as the `blocking` **bool** (`Humanoid.UpdateBlock` writes it
  through `ZSyncAnimation.SetBool`, never through `SetTrigger`), so `Patch_SetBool` watches
  that very call - once per block, at the moment the game itself decides your character
  blocks - and sends the creature's **third** attack instead: the middle one of the same
  pool, neither the basic nor the heaviest (`CreatureAttacks`, read by
  `CreatureTriggers.ResolveBlock`), through the very same `Send` (so it never reaches your
  own viking either, and your clone is fed by `GhostAnimation` as always). Measured: Troll
  `swing_logh` (6 s), Wolf `attack3`. A pool with nothing left to designate - two attacks or
  fewer, which is a goblin, a greydwarf or a draugr - has no third animation at all, and the
  block then simply stays a block (§ 7).
  The second name never reaches your own viking: `ZNetView` routes an `Everybody` RPC through
  `HandleRoutedRPC` *synchronously*, inside our own `InvokeRPC`, so `Patch_RPC_SetTrigger`
  drops that local half (some creature names are viking names too - every player spear throws
  under `spear_throw`, which is also the goblin's special attack), while your creature view is
  fed directly by `GhostAnimation`. Nothing is rewritten on reception: a machine rendering the
  creature plays the name it already has, a machine rendering a viking ignores it, and your
  own character never changes. Both reference sets are read from
  **active** objects: `Animator.parameters` returns an empty array while its `GameObject` is
  not active in the hierarchy, and a `ZNetScene` prefab never is (`m_prefabs` holds assets, in
  no scene at all) - reading the prefabs is exactly what used to keep this bridge silently
  dead. The player set comes from your live character, the creature set from one throwaway
  activated copy built per creature by `GhostFactory` (the weapon pool is read from the
  prefab, which needs neither); until both are non-empty **nothing is
  sent** rather than guess. What the bridge decided for each
  trigger is written once as `Attack bridge: <trigger> -> ...` in the log of your own client
  (§ 7). Everything else is untouched: the
  parameters both controllers share (movement, `jump`, `stagger`...) arrive as they always
  did, the rest is left to vanilla (§ 4.7).
- **Name above the head**: no, vanilla clients have no `Player` component for you (the
  server player list does display your name).

---

## 4. Known limitations (normal behaviour, not bugs)

1. **A vanilla client that already saw you** used to keep showing your old look: the game
   never re-creates an object when its prefab changes. The automatic *observer refresh*
   (§ 3, step 5) fixes that: the change becomes visible to everyone
   loaded after ~`RefreshHoldSeconds` (1.5 s), with nobody moving and nobody reconnecting.
   - If a client still shows the old look (very laggy connection, or
     `ForceObserverRefresh = false`): run **`disguise refresh`** (needs the server DLL),
     or use the old fallbacks (move ~100 m away, or a reconnection on either side).
   - The refresh **requires this DLL on the dedicated server**: without it nobody can relay
     the fake position. The disguise itself still works (vanilla clients do see the
     creature), but an already-loaded client keeps your old look until the fallbacks above;
     `disguise refresh` is a no-op without the server.
   - Tested 2026-09-29 (PC-only install): the other player does see the creature, but only
     once you move ~100 m away and come back (or after a reconnection).
   - A player **discovering you for the first time** always sees the creature directly (no
     refresh needed in that case).

2. **The disguise only applies to you.** The mod only touches your own player; other
   players (even modded ones) do not appear as creatures.

3. **Do not use it together with PlayAsMob.** `TenZly-PlayAsMob` is already present on
   the `serveur` profile: both mods fight over your player's prefab. Do not use the
   `playasmob` command while using `disguise` (and vice versa).

4. **Installing the DLL on the server is optional but recommended** (§ 1.b). Without it
   the disguise still reaches every vanilla client, but the automatic refresh,
   `ProtectSaveFile` and the server-side `Player` object are lost.

5. **Footstep effects are resolved against the creature.** `FootStep.RPC_Step` carries an
   index into the effect list of the object instantiated **by the machine that sent it**,
   and vanilla relies on every machine building the same prefab for a ZDO: a disguised
   player is a `Player` on machines running this DLL and a creature everywhere else, so
   the two lists disagree in both directions and vanilla throws
   `ArgumentOutOfRangeException` on every step (one per footstep, in the log of the
   player watching you). The mod therefore resolves step and landing effects against the
   **creature prefab's own list** (`Patch_FootStepFindBestStepEffect`) and tolerates an
   out-of-range index on reception (`Patch_FootStepRPCStep`) instead of throwing: vanilla
   observers play the creature's real footstep effect, machines running this DLL skip an
   effect they cannot resolve, and no mix of vanilla/modded observers can throw. Nothing
   is sent when the creature has no `FootStep` or no matching effect.

6. **Dying while disguised logs a warning on vanilla observers.** `Player.OnDeath`
   broadcasts `OnDeath` to every machine; a creature has no `Player` component to register
   it, so each vanilla observer logs one `Failed to find rpc method <hash>` per death (a
   warning, no exception). The broadcast is deliberately left alone: machines running
   this DLL hold a real `Player` for you and need it (`RPC_OnDeath` hides your visual).

7. **Only some of your animations reach the creature** (`Patch_SetTrigger`, `Patch_SetBool`).
   - **Played**: every trigger whose name both controllers share (movement, `jump`,
     `stagger`, `wakeup`, `spear_throw`...), plus **melee attacks**, which are re-sent
     under the creature's own attack name: its **normal** one for a normal swing
     (`swing_longsword` for a goblin), its **special** one for a secondary attack
     (middle click by default) - a goblin then really throws its spear on your `sword_secondary`.
     The creature is then animated from *your* swing, and from *your* block as well: a block
     never becomes a trigger (the game travels it as the `blocking` bool), `Patch_SetBool`
     picks it up and sends the creature's **third** attack for it - a Troll swings its log
     sideways on `swing_logh`, while a goblin, a greydwarf or a draugr has only two attacks
     to designate and simply stays put when you block.
   - **Not played**: ranged attacks (`bow_fire`, `crossbow_fire`, `staff_*`, and the
     spear's own secondary attack `spear_throw`, which is a throw - it only reaches a creature
     that has that very name, like the goblin, and that is the shared-name case above), a
     cancellation (`attack_abort`), the build-tool swings (placing, removing or repairing
     with the Hammer / Hoe / Cultivator), and gestures the creature has no equivalent for
     (`dodge`, `eat`, `interact`, `equip_hip`, `gpower`...) - the vanilla machine drops
     them silently, exactly as it drops any unknown parameter, and so it did before this
     mod existed. If the creature has no melee attack trigger the viking can borrow,
     nothing extra is sent either.
   - **Emotes never had a chance** on a vanilla observer, and this mod does not change
     that: they are not triggers at all but ZDO keys (`emote` / `emoteID`) played by
     `Player.UpdateEmote`, which a vanilla machine has no reason to run for you while you
     are a creature.
   - This costs **nothing on the other side**: the bridge is sent by the disguised
     player's own client, so only *that* client has to run a current build (§ 6). No
     vanilla client and no server DLL is involved.

8. **Flying is a rendering, not a movement** (`disguise fly`, § 2 and § 3).
   - Your player stays on the ground on purpose: the position the server runs the world
     with never changes, so nothing about it can be used to reach, chase or drop on
     anything from above. Your attacks, your collisions and your landings are the ones of a
     player standing where you actually are.
   - Which means the two views can disagree **while you are up there**: on another machine a
     creature hovering over a mob is still simulated on the ground below, so that mob bites
     the air, and what you hit is decided from the position the server knows. Landing
     (`disguise fly off`, or `FlyDisableKey`) puts both back in agreement instantly.
   - On **your own screen** the viking stays grounded unless `SeeYourDisguise` is on: with
     the option off you watch your own character, exactly as the server simulates it, while
     everybody else watches the creature fly.
   - **Only machines relaying with this DLL can raise it** (§ 1.b): your own view always
     flies, the others only do while the dedicated server runs the current build. A machine
     running this DLL renders you as a real `Player`, so what it raises is your viking - it
     has no creature of yours to draw (§ 4.7 always applied there too).
   - The **pose works the same way** (`disguise offset`, `disguise rotation`): the costume
     can stand and face somewhere else while your player stays exactly where it is, hitboxes
     and all - only what is drawn is moved and turned. It is memory only like the height
     (a restart puts it back to `0 0 0`), and it reaches the others only while the dedicated
     server runs this DLL (§ 1.b).

---

## 5. Clean uninstall (IMPORTANT)

**Never simply delete the DLL while `Enabled = true`.**

Procedure:

1. On your PC: `disguise disable` in the F5 console (your player becomes `Player` in the
   ZDO again, the marker is removed).
2. Also run **`disguise setFakePosition off`** if you ever used it: `cd_fake` / `cd_fpos`
   live in the ZDO like the marker, and a client with no DLL has no command left to clear
   them - the other maps would keep showing that stale position (§ 2).
3. Let the server run one save cycle (or restart the server).
4. Check with `disguise status` that the prefab is `Player` and the fake position is `off`.
5. Only then, remove the DLL from every location where you installed it (§ 1).

If you delete the DLL directly while `ProtectSaveFile = true`, the save still contains
`Player`: the world stays playable.
If you had set `ProtectSaveFile = false` **or** you never installed the DLL on the server
(`ProtectSaveFile` only exists there), the save contains the creature for your player:
**reinstall the DLL, set `Enabled = false`, run the server once** to reset everything back
to `Player`, then remove the DLL.

---

## 6. Rebuilding the mod

```powershell
dotnet msbuild "A:\_MODDING\Valheim\MODS\Disguise\Disguise.sln" -p:Configuration=Release
```

(Opening `Disguise.sln` in Visual Studio and building there is the same thing.)

> ⚠️ **Always build the `.sln`, never the `.csproj` alone**: `SolutionDir` is what lets the
> Jotunn build import `Environment.props` (Valheim / BepInEx / deploy paths). Built on its
> own, the project silently falls back to the default BepInEx folder and skips that config.

> ⚠️ The server copy warns (and is skipped, **without breaking the build**) while
> `valheim_server.exe` is running: **stop the dedicated server first** if you need the
> server DLL updated. The client copy is deployed either way.

Output: `Disguise\bin\Release\net48\Disguise.dll` (+ `.pdb`).

The build automatically places `Disguise.dll` **+ its `Disguise.pdb`** in
both locations:

- client: `...\com.kesomannen.gale\valheim\profiles\dev\BepInEx\scripts\`
- server: `...\r2modmanPlus-local\Valheim\profiles\serveur\BepInEx\plugins\_manual_install\`

(`DeployDisguise` target in `Disguise\Disguise.csproj`,
`DisguiseDeployClient` / `DisguiseDeployServer` properties).

Sources in `A:\_MODDING\Valheim\MODS\Disguise\Disguise\` - **one class per file**:

- `Disguise.cs` - the plugin class: constants (`PluginGUID`, `cd_disguise`, `cd_refresh`,
  `cd_hide`, `cd_rel`, `cd_fake` / `cd_fpos`), the log helpers and the lifecycle (`Awake` /
  `OnDestroy` / `Update`), which wires the other classes together. `OnDestroy` is a COMPLETE
  unload, not a formality: ScriptEngine reloads the mod by destroying the object holding every
  script plugin and loading the new DLL into a NEW assembly (fresh statics), so whatever this
  copy leaves standing is found by nobody - it lets go of the window and the mouse it captured
  (`DisguiseGui`), the clone on your own screen (`SelfView`), the pause menu's line
  (`PauseMenuEntry`), the terrain tiles it built to read the ground (`GroundProbe`), and every
  Harmony patch (`UnpatchSelf`), before the logger goes with the
  instance. Everything it does not share stays `private`.
- `DisguiseConfig.cs` - the thirty-six settings and their binding to `orax.Disguise.cfg`.
- `GameAccess.cs` - the only class reaching non-public game members: the reflection
  handles (its `s_*` fields) plus the helpers built on them (`GetNView`,
  `GetLocalPlayerZDO`, `GetZDO`, `BumpDataRevision`, `SetZdoIntSilent`,
  `SetZdoFloatSilent`, `GetMenuSettingsButton`, `HideMenu`) - the last two reading the
  pause menu's Settings button (to take the shape of) and closing the pause menu (§ 2),
  both PUBLIC in the game and reached through reflection all the same, the audit counting
  a public member exactly as it counts a private one.
- `PrefabResolver.cs` - every hash the mod uses (`cd_disguise` / `cd_refresh`, the game's
  own `level` key, the vanilla `Player` prefab hash, the configured creature hash) and
  `PrefabName`.
- `PrefabKind.cs` - the six categories of the list (`Static`, `Creature`, `Item`,
  `Building`, `Nature`, `Other`): one checkbox each in the window, one category per
  prefab. `Static` is the first test `KindOf` makes and the only one about movement instead
  of looks: a root without `ZSyncTransform` never gets a position again from anyone (§ 3).
- `PrefabCatalog.cs` - the catalogue both lists read: every **vanilla** prefab registered in
  `ZNetScene`
  - the technical limit of a disguise, since a vanilla client rebuilds you from **its own**
  prefab table by hash - sorted, each tagged with exactly one kind through `KindOf` (the
  first test that matches wins - `ZSyncTransform` first, then every other component read on
  the root like the `Character` test the list shipped with), plus `Names(filter, shown)` and
  `Counts(filter)`. Both ask for every category - the window takes every name and decides
  with its own checkbox row (its six categories, then Favorites beside them), so a category
  switched on later already has its names - and
  the walk reads the kind only for a caller that asks for it (the counts, a category mask):
  `KindOf` is up to ten component tests, the half of the walk that costs, and the window
  does that single pass itself, for its rows. One source, no disagreement. A modded name and
  an effect name (`fx_` / `sfx_` / `vfx_`) are cut out at the top of the walk, before
  anything is fetched or counted, and a name with **no mesh to be seen through** is cut the
  moment its prefab is fetched (`HasMesh`: a `MeshRenderer` or a `SkinnedMeshRenderer`
  somewhere under the root, the two types the preview counts before it frames a picture) -
  the creature's own attack helpers (`Abomination_attack1`, `wraith_melee`: registered `Item`
  prefabs the game draws for nobody), the zones a blob or Bonemass spawns, spawners and
  markers. They would be an empty picture in the window and an invisible viking in the
  world, and `disguise set <name>` still takes one typed by hand, as it does an effect.
- `VanillaPrefabs.cs` - the vanilla test behind both lists, and the only place that decides
  it: Valheim describes its **own** assets in `valheim_Data\StreamingAssets\SoftRef\manifest`
  and `manifest_extended`, plain text with one `path in bundle: Assets/.../Ghost.prefab` line
  per asset, and a mod never lands in them (a mod loads its bundle from its own plugin folder
  through `AssetBundle.LoadFromFile`, which the game never indexes). The file names of those
  lines - a prefab is named after its file - are read once per session into a case-insensitive
  set, `Contains(name)` answers from it after that, and neither file readable means no
  filtering at all (one warning says so, the lists stay as complete as before).
- `PrefabFavorites.cs` - the marks behind the Favorites box: one comma-separated line in the
  config (`6 - GUI` / `Favorites`) parsed only when it is not the line the two lists already
  hold (so `Contains` is free where it is asked per row and per frame), and `Toggle`, which
  writes the line back whole - the names already in it keep their place, and a name taken off
  goes out of every place it was repeated. A favorite is a mark and never a category: the
  kind PrefabCatalog gave the prefab stands, and `GuiCategories.ShowRow` is where the two
  filters meet.
- `DisguiseApplier.cs` - `HasMarker` / `ApplyTo` / `InstantiateAs`: writes the marker, the
  **level** of the costume (the game's own `level` key, on YOUR zdo only - another player's
  level is theirs to publish) and swaps the network prefab.
- `EquipmentMask.cs` - the equipment display options (`3 - Display`): swaps the equipment
  hashes out of the outgoing packet, out of the save file, and back into the frame the
  modded machines render (§ 2).
- `HealthMask.cs` - the costume's health on the machines that only render it (§ 3): writes
  the costume's own maximum into the `health` key for the duration of `ZDO.Serialize` and
  puts the real one back right after, relaying machine only, so a vanilla client draws a
  whole costume instead of one wearing your hit points as damage. Silently, no DataRevision
  bump, and never through `ZDO.Save`. A **creature's** maximum is the one a creature of its
  level has - base x level, the way the game gives every non-player Character its health -
  while a piece or a vehicle, which owns its hit points through `WearNTear`, keeps its own
  whatever the level says.
- `AllyView.cs` - the other half of `3 - Display`: publishes the health-bar relation
  (`cd_rel`) and writes it into `ZDOVars.s_tamed` for the duration of the packet, so a
  vanilla client draws your creature as an ally or as an enemy (§ 2).
- `ObserverRefresh.cs` - the `cd_refresh` window and the fake far-away position the
  modded server relays during it.
- `FakePosition.cs` - the fake map position (`disguise setFakePosition`): what the owner
  publishes (`cd_fake` / `cd_fpos`), and the swap the server runs over the player list
  before it writes it, always restored right after (§ 2).
- `FlightControl.cs` - the flight (`disguise fly`, § 2): the local state (on / off, height),
  the four key shortcuts read straight from their `KeyboardShortcut` entries, the scroll
  wheel while the flight owns it, the throttled
  publication of the height (`cd_fly`),
  and the raise the relaying server adds to the outgoing packet header - server only,
  creature only, handed back as a copy so nothing has to be restored (§ 3). It also carries
  its two windows: the outgoing packet (`BeginSend` / `EndSend`, `Patch_SendZDOs`, opened
  side by side with those of `PositionHold` and `RestOffset`) and the camera zoom (`SuspendZoom` /
  `ResumeZoom`, `Patch_UpdateCamera`).
- `PositionHold.cs` - the band the outgoing position of a disguised player is relayed inside
  (§ 3): the owner's rigidbody jitter would otherwise travel to a vanilla client, where
  `ZSyncTransform.SyncPosition` chases it with a lerp and turns it into a shiver of a few
  millimetres. Server only, packet only, handed back as a copy, gated on the `cd_disguise`
  marker (anything else - the world's own rigidbodies - is relayed as the game has it) and
  skipped while the observer refresh fakes its far-away position.
- `RestOffset.cs` - the ground rest of a costume (§ 3): the height its own geometry asks
  for to stop digging into the ground - everything it hangs below the player's feet, plus
  what the plane fitted under its footprint cannot explain, plus the clearance. Measured
  once per prefab (box, sphere, capsule and mesh colliders; the meshes only for the log),
  skipped for what cannot fight back, the ground read four ways in order - our own ray over
  the terrain layer, the probe that recognises terrain by the component it carries behind
  it, the loaded heightmap, and the zone `GroundProbe` builds where nothing else exists -
  the last three reached through `GameAccess` so no new game member is named, with a
  self-test line for a read that answers with nothing - and added as a copy to
  the outgoing position on the relaying server and to the clone of your own screen,
  recomputed there at most ten times a second. The zone is only ever built while relaying,
  so it can never be built against a position this machine made up.
- `GroundProbe.cs` - the zone a machine never built (§ 3): keeps a terrain tile standing
  where the costume is read, on a machine - a dedicated server - whose zones are generated
  in *ghost* mode and have their temporary root destroyed the moment they exist. Rebuilds
  it exactly as `SpawnZone` does (the game's own `m_zonePrefab` at the zone position,
  behind `HeightmapBuilder.IsTerrainReady`, which is what also queues the terrain build -
  and waits for it, bounded and one wait open at a time, because the data a tile is built
  from is consumed by that build and the packet may not leave without it), reads it once
  with `Heightmap.GetWorldHeight`, and destroys the root again after ten seconds without a
  read. The root is deliberately left out of `ZoneSystem.m_zones`, where
  the game puts every zone it spawns together with the lifetime that expels it
  (`UpdateTTL`, four seconds without a poke): unknown to that register, the zone is neither
  expired by the game nor mistaken for a loaded one by `IsZoneLoaded`, and only this mod's
  own lifetime ends it. The heightmap is a different registry - it adds itself when it is
  enabled and removes itself when destroyed, so the heightmap read finds the zone from the
  first moment it exists, and the ray and the probe behind it find it by the collider the
  prefab carries. The zone id is the body of `ZoneSystem.GetZone` / `GetZonePos` copied
  rather than called, and every handle it needs lives in `GameAccess`, so the audit count
  does not move.
- `CreatureTriggers.cs` - the animation bridge: resolves a local trigger against the
  creature's own controller (and against `Player_animator`, so a companion name can only
  ever be played by the creature), decides which of the creature's attacks to play (normal
  for a normal swing, special for a secondary attack - read from the weapon in your hand -
  and, for the block key, the middle attack of the pool through `ResolveBlock`), used by
  `Patch_SetTrigger` and `Patch_SetBool`. It reads the player set from
  the live local player and the creature set from a throwaway activated copy (both reference
  sets are unreadable on a `ZNetScene` prefab), and owns the one-line `Attack bridge:` trace
  of its decision (`Trace` / `TraceBlock`), the only place where that decision is visible.
- `CreatureAttacks.cs` - designates those three attacks from the creature's weapon pool (the
  `Humanoid` of its prefab): melee with the shortest `m_aiAttackInterval` = normal, longest
  = special (null when the pool holds one attack), the lower median of what is left over =
  block (null when the pool holds two or fewer), restricted to the triggers the probe
  really read. Plain serialized fields, so unlike `Animator.parameters` it needs neither an
  activation nor an instantiation.
- `DisguiseCommand.cs` - the `disguise` console command and its sub-commands, plus what the
  window needs from it so the console and the window can never disagree:
  `ApplyCreature(name, reason)` (config + re-application, printing nothing: each side
  reports its own way on top of it). `disguise list` is `PrefabCatalog.Names(filter, null)`
  plus `PrefabCatalog.Counts(filter)` - every category, whatever the checkboxes of the window
  have on. `Execute` and `Say` both take the `Terminal` behind the
  command - the one the command was typed in for the console, `null` for a button of the
  window - and `Say` with no terminal writes the line to `GuiStatus` instead: same text,
  only the place where it is read differs. `Run(sub)` is what a button calls.
  `TryParseCoordinate` is the number parser the pose shares with the window (`GuiPose`),
  because a value has to mean the same thing wherever it comes from.
- `DisguiseGui.cs` - the Disguise window (`GuiKey`, `disguise gui`, `Escape`, § 2): the chord
  read once per frame, `GuiWindow.RefreshState` once per frame while the window is on screen
  (the line under the title carries the flight and the height it lifts the costume by, two
  values that move by themselves), the Escape that closes the window and stamps the frame
  `Patch_MenuShow` reads, the single window that exists at a time, the `BlocksInput` flag both
  input patches read (pointer on the window **or** filter field focused **or** one of the six
  pose fields focused), the handover of the
  mouse through Jotunn's `GUIManager.BlockInput` (balanced
  one `true` per `false`, only inside the `main` scene) and the healing of a window destroyed
  by a scene change, which cannot release that request by itself - and the place of the
  window: `RememberPosition` reads the panel's offset every frame (the drag moves it on its
  own, so looking at it is the only way to keep it), `SavePosition` writes it into `WindowX` /
  `WindowY` once and only when it changed, both called from `DestroyWindow` and from that
  healing, so even a window taken by a scene change keeps the corner it had been dragged to.
- `PauseMenuEntry.cs` - the `Disguise` line of the pause menu (§ 2): a clone of the game's own
  Settings button, taken with the shape and the place it has (right after Settings, by sibling
  index, inside the very parent the original lives in), relabelled, switched on, and given a
  click that is REPLACED rather than added - the whole `Button.onClick` event goes, prefab
  listeners included, which `RemoveAllListeners` alone would have left standing (Settings
  opening behind the window the entry just opened). The click closes the pause menu through
  `GameAccess.HideMenu` - `Menu.Hide`, the game's own hand, which also unpauses and hands the
  cursor back to the window about to open - and then opens that window with the very
  `DisguiseGui.Toggle` the chord uses, so the two ways of opening it can never disagree. The
  line is only there while `ShowMenuEntry` (`6 - GUI`, on by default) is: the value is read at
  every open of the menu and applied at once when it changes, which builds the line or takes it
  out - DESTROYED, not hidden, and with the two links it had in the gamepad's chain rewound
  first to the ones the menu wrote itself (a chain pointing at a button that no longer exists
  is still followed, and a D-pad then goes nowhere). Built the first time the pause menu opens
  (one `Instantiate` for a whole world, a new one after a scene change, the null test being
  Unity's own - true of a destroyed object as well), and `WireNavigation` puts the entry back
  into the gamepad's up / down chain after every rewire of the menu's own, idempotently: a
  chain that already holds the entry is left as it is. `Cleanup` is the same removal for the
  other reason to make it - the plugin unloading, `OnDestroy` running while ScriptEngine
  reloads the mod into a fresh assembly (§ 1): the clone stands in the GAME's own menu, where
  nothing else would ever destroy it, so the copy loaded next would meet it and draw a second
  one beside its own.
- `GuiWindow.cs` - what goes INSIDE the window and where: Jotunn's wood panel (draggable, and
  `Place` first puts it where the config says - `WindowX` / `WindowY`, an offset from the
  middle of the screen, `0`/`0` being that middle and the default - then keeps it on screen
  with the very rule the drag follows, so a hand-typed place or another resolution cannot
  strand it outside the monitor), the
  title with the `GuiKey` chord beside it (`DescribeChord`: the config value, main key first,
  or `Not set`), the state line directly under the title and the checkbox row in
  the band under that line (Favorites first, the six categories alphabetically - this class only
  tells `GuiCategories.Build` where they sit), the centred labels, the state line
  (`Prefab: <name>   -   disguise <on|off>   -   Flight <on|off>   height: <y>   -   size <w> x <h> x <d> m` - the
  height is the raise the flight owns, in metres with one decimal, `0.0` while it is off and
  never below 0 (`FlightControl` clamps it to 0..100); it is named after the flight and not
  after the pose, whose own `Offset` row is a different thing; the size is width, height and
  depth of the meshes the configured prefab really draws, measured once on the prefab asset
  and cached against its hash (`PrefabResolver.CreatureSize`, which shares the preview's rule
  with it and takes the box in the costume's own space, so the asset's parking place has no
  say), left out while there is no mesh to
  measure; the Text
  is best-fit, so a long prefab name shrinks instead of running past the window, and
  `RefreshState` writes it only when the words changed: once per frame while the window is
  open, plus every click - and it hands the six pose fields their sync on the same pass), the
  corner of the two pose rows (`GuiPose.Build` gets the band the layout leaves under the
  list BLOCK - the scroll view is only its lower part, the filter field and its gap being
  part of the block too, measured through `GuiCreatureList.BlockHeight`, which is what keeps
  the rows off the last rows of the list; the panel is 670 px tall because of them and of the
  two lines that close it, the status line being anchored to the bottom edge (nothing above
  moves when the panel grows, and `StatusY` is the number that keeps its own box where it was
  across the 40 px it grew by for the help line)), the
  row of five command buttons under the picture (Favorite first, then Enable/Disable,
  Refresh, Own view, Flight; each one runs its sub-command through
  `DisguiseCommand.Run`, then `RefreshState` rewrites the state line and the labels of the
  two toggles among them - the Favorite one from the configured
  prefab and its mark, the other from the config; their text is best-fit, five buttons now sharing the width of the picture), the
  **band** under those buttons: the **level row** first, at the picture's own left edge
  (`LevelRow`: the word `Level`, then `-` and `+` around the number - each button runs
  `disguise level <n>` through the very same `DisguiseCommand.Run` path as every button of
  this window, the number is rewritten per frame by `RefreshState` like every other label,
  `SyncLevelButtons` stops the two at the ends of the span the game itself keeps a creature
  inside, 1 to 9, and the whole row's width is the shared `LevelRowWidth`), then the
  **sparkle box** to its right (`SparkleBox`: the very control `GuiCategories.BuildBox`
  puts in the row above, starting after `LevelRowWidth` + a gap of the band and centred in
  the 94 px the right half has between the buttons and the status line - its listener writes
  `HideItemSparkle` and the cache `RefreshState` compares against, so a value typed in
  ConfigurationManager is pushed back into the box while a click never is), the
  status line below them, the **help line** closing the window under that one (`GuiHelp.Build`
  - the words of every control are handed to `GuiHelp.Watch` as it is built: Close, the chord,
  the state line, the five buttons, the sparkle box and the level row here, and the same call made by
  `GuiStatus`, `GuiPose`, `GuiCreatureList` and `GuiCategories` for their own), and the
  coordinates of the list block. It only builds,
  it never keeps the window alive nor talks to the console - both stay in `DisguiseGui`.
- `GuiText.cs` - one thing the whole window does over and over: put a word on a label, and
  write it only when the word really changed (uGUI lays a text out again on EVERY
  assignment, an identical one included, so a label refreshed every frame would re-measure,
  re-wrap and rebuild its mesh for nothing). One overload for each of the two text
  components the game has - uGUI `Text` and TextMeshPro share no base carrying `text` - and
  one of them takes the text as a builder, called only when the label is there to show it:
  that is how `GuiWindow.RefreshState` keeps `DescribeState`, `DescribePower` and
  `DescribeFavorite` from running for a window a scene change already destroyed. A null
  label is a label that scene change took, and costs nothing.
- `GuiMetrics.cs` - the two measures the window shares with the lines around it: the 800 px
  every line spanning the window is cut to (the 860 of the window minus the margin it keeps
  on both sides - the help line, the status line and the state line all line up on it), and
  the 40 px of one row (a command button, a help line, a line of the list). Both names
  replace two private constants each plus one raw literal, so the measure a player sees
  line up cannot drift apart in four files again; the status line keeps its own 44 (it sits
  under the window's frame) and the title row its `TitleRowY`, in `GuiWindow`.
- `GuiCategories.cs` - the row of seven checkboxes: Jotunn's `CreateToggle` box (the
  Valheim checkbox, orange marker) with a label measured to its own text
  (`Text.preferredWidth`, so the air before the next box is the same whatever the word), the
  `Show*` entry of each kind as the only state of the last six - a choice survives a
  restart, and `Sync` pushes a value typed in ConfigurationManager back into its box without
  reopening, and reacts to the `Favorites` line too (marks come and go while the window is
  open) - and `Refilter` on every change, which first builds the rows the boxes just started
  OWING (the opening only builds what they owe already: the six categories, or the marks
  when `Favorites` was already ticked) and then re-runs the filter over them. The row is
  drawn **Favorites first, the six kinds after it in alphabetical order** (`s_kinds` - the
  enum keeps the order `disguise list` prints in, and no box reads a kind by its number any
  more); the first box reads `ShowFavorites`, and `ShowRow` holds the one rule of a row: the
  marks alone while it is on, the box of the kind otherwise. A category and a mark part
  company in one thing
  only, the scroll: a category is another list to look at, so the list goes back to its first
  row (`Refilter`), a mark is one row more or less behind the pointer, so the list keeps the
  place it was in (`RefilterInPlace`). One box of that row is a method the window borrows:
  `BuildBox` builds a single checkbox (box, label measured to its text, the label carrying the
  Button that flips the box) and hands the cursor on, so the row is seven calls of it and the
  sparkle box of `GuiWindow` is the eighth with its own listener - the caller giving each one
  the help to show on the bottom line, which `BuildBox` puts on BOTH halves of the checkbox:
  the box and its label are two objects, and the event system stops at the first one that
  handles the pointer, so the label - the one that carries the Button - is where the pointer
  usually is.
- `GuiCreatureList.cs` - the prefab list: Jotunn's filter field with the **Clear** button at
  its right (the field and the button share the width of the block, rows keep all of theirs;
  the button empties the field through `SetTextWithoutNotify` and asks for `Refilter` itself
  - one path, taken once, instead of depending on whether a write notifies), and Jotunn's
  scroll view, one row per vanilla prefab built **at open for what the boxes owe** - the
  rows a category (or the Favorites box) starts owing later are made by `EnsureShownRows`,
  the first time they are owed and at the sibling index that keeps the sorted order, no row
  ever destroyed;
  the text filter and the checkboxes then only show and hide with `SetActive`, so typing
  never creates anything. The kind of every row is kept in a third parallel list next to the
  name and the body (which checkbox owns it; the body is null while its category has never
  been shown), `Refilter` - what a checkbox and a changed `Show*` call: build what a
  category owes, then re-run the filter over the text already typed and go back to the first
  row - and `RefilterInPlace`, the same work with the scroll left alone, which is what a
  changed `Favorites` line calls. The **place of the list** is kept between openings as well:
  the window is destroyed as a whole when it closes, so `Sync` remembers the offset of the
  content every frame and `PutBackOffset` writes it back into the next window - a couple of
  frames after the build, the rows needing one layout pass before a position can hold in
  them. The star of a favorite is the label's own text (`Label` - a gold `#FFD700`
  `<color>` tag at the right of the name) rewritten row by row by `SyncFavoriteStars`
  whenever the `Favorites` line changes, hidden rows included. The orange highlight of the
  configured prefab, a row click routed to
  `DisguiseCommand.ApplyCreature`, and
  `FilterFocused` - the half of `BlocksInput` that hands the frame to the UI while the
  player types. Each row is given the listener of the preview (`GuiPreview.WatchRow`), which
  is how the picture knows what the pointer has been on - and, since `GuiHelp`, how the bottom
  line knows what to say about it (the same component writes both). The filter field and the
  Clear button are watched the same way. `BlockHeight(listHeight)` answers how tall
  the whole block is for a list of that height - the scroll view is only its lower part, the
  filter field and its gap being part of the block too - which is what GuiWindow measures the
  corner of the pose rows through.
- `GuiPose.cs` - the two rows under the list: the six editable fields holding `PoseControl`
  (Offset X/Y/Z in world units, Rotation X/Y/Z in degrees), and the whole difference between
  a box and a value. A change applies **as it is typed**, each field parsing through
  `DisguiseCommand.TryParseCoordinate` - the parser the console shares, so `1.5` and `1,5`
  both land - and keeping the value it had while the text is not a number yet (empty, a lone
  `-`, half a typed number), which is what stops the costume from jumping somewhere the
  player did not ask for. `Sync`, called from `GuiWindow.RefreshState` once per frame, writes
  a value back only when the words really differ and **never in the field being typed in**,
  so a value set from the F5 console lands in the window without disturbing a half-typed one;
  `Focused` - the second half of `BlocksInput`, beside `FilterFocused` - hands the frame to
  the UI while one of the six has the focus. The two labels and the six fields all carry their
  help on the bottom line (`GuiHelp.Watch`), the label saying what its whole row is for.
- `GuiRowHover.cs` - the listener on every row: pointer in, show its creature - and that one
  stays until another row comes under the pointer, so the pointer can leave the list for the
  buttons and the picture keeps showing what was just looked at; and, since `GuiHelp`, pointer
  in also puts that row's help on the bottom line and pointer out puts the idle line back -
  the exit being the one thing the picture never needed (it keeps what it was told) and the
  help line does, because leaving one row for the next must not leave the first one's words
  up. Two pointer interfaces and no other,
  on purpose. Unity's `EventTrigger`
  does the same job with two entries, but it also implements scrolling and dragging - and the
  event system only walks up to the FIRST object of a hierarchy that handles an event. An
  `EventTrigger` on a row therefore swallowed the wheel and the dragging of the list (the walk
  stopped on the row, the `ScrollRect` above it never heard of either, and only the scrollbar
  - which no row stands over - kept working), while an object without the interface is simply
  passed over. The two interfaces below cost nothing of the kind: the row keeps its own Button
  handlers (they are components of this very object, and Unity calls all of them), and a wheel
  over a row still walks up to the `ScrollRect`.
- `GuiStatus.cs` - the line under the pose rows: the hint about clicking a row while
  no button has been pressed, and the answer of the last command button after that. One line
  holds one message, so `DisguiseCommand.Run` clears it before running and only the first
  line written after that (the headline) is kept; the console, which has a terminal behind
  it, keeps every line. It is watched like any control (`GuiHelp.Watch`), so hovering it
  says what it is; the help line closes the window UNDER it.
- `GuiHelp.cs` - the line at the very bottom of the window, under the status line: what the
  element the pointer is on is FOR, and an idle line (`Drag the panel to move the window -
  this line describes whatever the pointer rests on.`) while it rests on nothing that
  describes itself. Exactly two events write it - `Show` from the element entered, `Clear`
  from the one left - and the words are OWNED: a `Clear` is only obeyed from the component
  the line belongs to, so the order Unity sends the two events in cannot leave a stale line
  up, and the words are compared before they are written, so hovering one control for a
  whole frame costs nothing. `Watch` is the other half of the class: it puts a `GuiHover` -
  one small component with `IPointerEnterHandler` and `IPointerExitHandler` and no other
  interface, the same rule `GuiRowHover` was written with (Unity's `EventTrigger`, which
  does this job with two entries, also implements scrolling, dragging and the start of
  both, and the event system only calls the handlers of the FIRST object of a hierarchy
  that handles an event - an `EventTrigger` on a row swallowed the wheel and the dragging
  of the list) - on the element, and turns the element into a hit target as well: Jotunn
  switches raycasting off on every label, and an object the pointer cannot reach is an
  object whose help can never be shown. The rows of the list need no second component - they
  reuse `GuiRowHover`, which already listens to the pointer for the preview - and nothing
  else changes hands: an event an object does not handle itself still walks up to the panel,
  which owns the drag, and a Button keeps its own Enter and Exit (every component of one
  object is called). Dropped with the window (`Forget`, beside `GuiStatus`'s).
- `GuiPreview.cs` - the right half of the window: the `RawImage` the preview is drawn into
  (a hit target for the help line and for nothing else - `GuiHelp.Watch` makes it one, it
  handles Enter and Exit alone and every other event walks up to the panel, which owns the
  drag; hidden while there is no texture - a `RawImage` with no texture draws
  a white plate, and a hidden Graphic is not raycast either, so the picture answers the
  pointer only once it is drawn) and the rule that decides what is in it: the last row the pointer was on, the
  configured creature before any row was pointed at in this opening, the change held back
  0.15 s so that a pointer sweeping the list builds one copy instead of one per row crossed -
  and two more reasons to build the copy again: the sparkle option (`HideItemSparkle`) and
  the level (`CreatureLevel`), both read by the factory at build time - a value that moves
  while the picture stands shows the creature once more, and only when it can matter (an
  item for the sparkle, `GhostFactory.HideSparkle`; a level above 1 for the level,
  `LevelSkin`).
  The pointer reaching a row is `GuiRowHover`, one component per row, which now also writes
  that row's help on the bottom line.
- `CreaturePreview.cs` - the stage the preview is filmed from: the free layer, the camera of
  its own and its `RenderTexture`, the two lights, the inert copy of the creature
  (`GhostFactory.BuildInert`), the framing measured from the bounds of its meshes and fitted
  along both axes (so the object itself fills the picture, whatever its shape or size), the
  turntable, and `Forget` - texture released, layer given back to the cameras and the lights -
  called with the window closing.
- `LevelSkin.cs` - the skin of a star level, on the two copies this mod builds itself: the
  clone your own screen draws (`SelfView` / `GhostFactory.Create`) and the picture in the
  window (`CreaturePreview`), which are inert copies the game's own skin cannot dress -
  `LevelEffects` puts it on in `Start`, through the `Character` above it, and the factory
  strips every `Character` before a copy wakes. So this calls the very method the game calls
  (`SetupLevelVisualization`: the level's scale, its alternate skin object, its tinted
  material) and lends the copy the one thing the method asks of its `Character` - the
  prefab's own, of which the method reads nothing but the name of its gameObject (the key of
  the material cache, shared with the real creatures of the world). Everything goes through
  reflection BY NAME, the way `HealthMask` reads `m_health`: a rename in a game update costs
  the base look, never an exception on your screen.
- `Patch_CreateObject.cs`, `Patch_ZDOLoad.cs`, `Patch_PlayerOnSpawned.cs`,
  `Patch_ZDOSave.cs`, `Patch_ZDODeserialize.cs`, `Patch_SendZDOs.cs`,
  `Patch_CreateDestroyObjects.cs`, `Patch_FindSectorObjects.cs`,
  `Patch_ZDOSerialize.cs`, `Patch_ZDOSerializeRelation.cs`, `Patch_ZDOSaveEquipment.cs`,
  `Patch_VisEquipment.cs`, `Patch_FootStepFindBestStepEffect.cs`, `Patch_SetTrigger.cs`,
  `Patch_SetBool.cs`, `Patch_ZDOGetPosition.cs`,
  `Patch_RPC_SetTrigger.cs`, `Patch_FootStepRPCStep.cs`, `Patch_WritePlayerInfo.cs`,
  `Patch_UpdateCamera.cs`, `Patch_GetMouseScrollWheel.cs`, `Patch_TakeInput.cs`,
  `Patch_SetMouseLook.cs`, `Patch_MenuShow.cs`, `Patch_MenuNavigation.cs` - the
  twenty-five Harmony patches, one
  `internal static` top-level class each. They reach the features through the `internal`
  members of the classes above (fields they swap in/out while a packet or the player list
  is being written, helpers such as `HasMarker` / `ApplyTo` / `InstantiateAs`) and log
  through `Disguise.LogError(...)` - `s_instance` itself never leaves the plugin class.
  `Patch_CreateDestroyObjects` additionally owns the `s_inCreateDestroy` scope flag read
  by `Patch_FindSectorObjects`, and `Patch_SetTrigger` the `s_applyingCompanion` scope flag
  read by `Patch_RPC_SetTrigger` plus the shared `Send` helper that `Patch_SetBool` also
  uses to reach every machine with the block key (§ 3). `Patch_UpdateCamera` and
  `Patch_GetMouseScrollWheel` are the two halves of the camera window: the first opens it
  around `GameCamera.UpdateCamera`, the second empties the scroll that one call reads (§ 3).
  `Patch_TakeInput` and `Patch_SetMouseLook` are the two halves of the Disguise window
  (§ 2): the first closes `Player.TakeInput` for the frame, the second hands a zero vector to
  `Player.SetMouseLook` - both only read `DisguiseGui.BlocksInput` and call nothing, so
  neither can throw into `Player.Update` nor into `PlayerController.LateUpdate`.
  `Patch_MenuShow` is the third patch of that window (§ 2): a prefix on `Menu.Show` that keeps
  the pause menu off the Escape which closed it, reading `DisguiseGui.IsOpen` and the frame
  `DisguiseGui.Tick` stamped - it calls nothing either, and so cannot throw into
  `Menu.Update` - plus a POSTFIX on that same method which hands the menu just opened over to
  `PauseMenuEntry` (Harmony runs a postfix even when a prefix stopped the method itself).
  `Patch_MenuNavigation` is the twin of that postfix on the private `Menu.UpdateNavigation`,
  named rather than referenced, and calls nothing but `PauseMenuEntry.WireNavigation` - which
  is what keeps the gamepad's chain running through an entry the menu knows nothing of.

(to be read together with the game decompilation in `T:\temp\opencode\vh_dec`).

### Visibility audit (re-run after every build)

The project compiles against `valheim_Data\Managed\publicized_assemblies\*.dll`, where
**every** member is made public. The game loads the *original* assembly instead, where
`private` / `protected` stay: a direct reference to such a member makes the plugin fail
at runtime with `FieldAccessException`. Already hit with `Terminal.commands` (protected),
`ZDO.m_prefab` (private), `ZDO.IncreaseDataRevision()` (private), `Character.m_nview`
(protected), `VisEquipment.m_nview` (private) and `FootStep.FindBestStepEffect` (private)
- all reached through reflection (the `s_*` fields in `GameAccess.cs`); `FootStep.m_nview`
(private) is read back with `GetComponent<ZNetView>()`, exactly like `FootStep.Start` does.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File "A:\_MODDING\Valheim\MODS\Disguise\audit_visibility.ps1"
```

Expected output: `Game members referenced: 83   ISSUES: 0` (exit code 0).
On exit code 2, replace every listed member with reflection
(`FieldInfo` / `MethodInfo` / `AccessTools`).
Note: `ZDOMan.ZDOPeer` (nested type of the game) is **not public**, so the mod never
mentions it in a signature - Harmony patch targets are only strings (`nameof`), and the
outgoing packet is patched through `ZDOMan.SendZDOs` without any of its parameters.
`Humanoid.s_blocking` (private) is not used either: `Patch_SetBool` compares the parameter
against `Animator.StringToHash("blocking")`, the very expression the game uses to define
that constant, so the block key adds no game member at all - the count was unchanged by it.
The flight is written the same way: `cd_fly` travels as an **int** (the `ZDO.Set` /
`GetInt` overloads the mod already used - `ZDO.Set` has no float overload it could reach
for free), `position` is swapped through the very `FieldInfo` of `Patch_SendZDOs`, and
`ZNet.IsServer()` was already referenced - so it adds no game member either. The `Static`
category follows the same rule: `ZSyncTransform` is public, but the
test is `GameObject.GetComponent("ZSyncTransform")` - Unity's own string overload, which
answers exactly the same question on a prefab asset - so the newest category of them all adds
no game member either.
Those three features cost **nothing**, and so does most of what came after them: the ground
rest measures geometry with Unity's own `GetComponent` / `Collider` / `Matrix4x4` and casts
the game's terrain ray itself with `Physics.Raycast` - `ZoneSystem.GetGroundHeight` is no
longer referenced at all, which took the two members it cost back off the count - while the
heightmap behind it is reached through `Assembly.GetType("Heightmap")` and `GetMethod`, so
neither the type nor the method is named and the audit cannot see either; and the health
swap writes through `ZDOExtraData.Set(ZDOID, Int32, Single)` - the float twin of the silent
int write `GameAccess` already used - for **+1** member over the 82 the count stood at.
The zone a dedicated server never built is the same story taken one step further: every
member it touches is **public** in the game (`ZoneSystem.instance`, `ZoneSystem.m_zonePrefab`,
`HeightmapBuilder.instance` and `IsTerrainReady`, `Heightmap.IsDistantLod`, `m_width`,
`m_scale`, `GetWorldHeight`) and it still reaches them through reflection, because the
audit counts a public member just as it counts a private one - while the zone id is the
body of `ZoneSystem.GetZone` / `GetZonePos` reimplemented in `GroundProbe` rather than
called, so the fourth read adds no member either and the count stands at 83.
The pause menu entry is that same rule once more: `Menu.m_settingsButton` (public - read to
clone the shape of the entry from it) and `Menu.Hide` (public - called to close the pause
menu with the game's own hand) are both reached through `GameAccess`, and the two patches
name their targets with `nameof` (`Menu.Show`, the second one a **private** method, named
rather than referenced) so that neither adds a member either - the count still stands at 83.
Everything else of the two fixes (`ZDO.GetPrefab`, `ZDO.GetFloat`, `ZNetScene.GetPrefab`,
`ZNet.IsServer()`, `prefab.GetComponent("...")`, `prefab.GetComponent<Character>()`) was
already referenced somewhere else, and neither fix added a Harmony patch: both run inside
ones that already existed (`Patch_ZDOSerialize`, `Patch_ZDOGetPosition`, `Patch_SendZDOs`),
so the count of twenty-four stays correct.

---

## 7. Troubleshooting

> **Before reading any `Info` line quoted below** (`Attack bridge: ...`,
> `Observer refresh ...`, `Map position spoof ...`, `Local player (ZDO ...)`, the output of
> `disguise list`, ...): those are
> only written while **`Logs = true`** (§ 2) - **off by default since 0.1.0**, a working
> install keeping its log quiet. Run `disguise logs on` first - or set it in the `.cfg`; the
> command writes the value back, so it stays for the next start. Warnings and errors are
> written whatever that value is.

| Symptom | Check |
|---|---|
| `SymbolsNotFoundException: No symbol found for file ...` | `Disguise.pdb` missing next to the DLL in `BepInEx\scripts\` → rebuild (§ 6) |
| `FieldAccessException: Field ... is inaccessible from method ...` | A game member is `private`/`protected`: never access it directly, use reflection, then `audit_visibility.ps1` (§ 6) |
| Nothing changes for the others | `disguise status` (wrong prefab?), then look for `Loading [Disguise 0.1.0]` in the **PC** log. If they only miss the *change* and not you: no refresh without the server DLL (§ 4.1) |
| `disguise status` says "NOT FOUND" | Wrong prefab name → `disguise list` |
| The console scrolls the list away, or you want to keep it | `disguise list` writes the **whole output to the BepInEx log** as well while `Logs = true` (§ 2), where nothing scrolls away - `486 prefab(s):`, then `kinds: ...`, then four names per line, exactly as the console prints them (`disguise logs on` first) |
| You appear as a viking to others | They already saw you before → run `disguise refresh`, or move ~100 m / reconnect (§ 4.1) |
| **You are invisible to a vanilla client** | Only possible with the DLL on the server: the relay is stuck on the fake position (the owner never sent `cd_refresh = 0`): the server log must show `Observer refresh flag cleared on ZDO ...` after every change. Restart the dedicated server (the flag is stripped at world load) with the current build. |
| Others see your new look too late | Normal: ~`RefreshHoldSeconds` (1.5 s); raise it in the `.cfg` on a laggy connection |
| Fake position warning in the server log | `Observer refresh flag still set after 5 s`: owner vanished mid-refresh; it stopped by itself |
| An enemy name + health bar stays on screen (even after `disguise disable`), and the log fills with `NullReferenceException` on `EnemyHud.TestShow` / `Character.IsTamed` | A game component survived the clone's strip: the clone has to be discarded, not activated (§ 2, own view). Deploy a current DLL (§ 6) and **restart the game** - a dead entry already in `Character.s_characters` is not healed at runtime |
| `ArgumentOutOfRangeException` in `FootStep.RPC_Step` in the log of the player **watching you** | That client received a *player* footstep index for a creature: the disguised player's DLL is older than the footstep translation (§ 4.5) → deploy a current build (§ 6) and restart |
| Your equipment is invisible to the other players although every `Hide*` option is `false` | A relaying server running an **older** DLL masks with its own `[3 - Display]` config (`Hide* = true` by default): deploy the current build on the server and restart it (§ 1.b). Since `cd_hide` your own config is the only one that decides |
| Your creature keeps the red enemy bar although `ShowAsAlly = true` | `cd_rel` is published by the client running the mod: restart **that** client with the current build (§ 6). An older server DLL is harmless here, it relays the flag exactly as it receives it |
| Your creature stands still while you swing (its attack never plays) | The bridge is sent by **the disguised player's own client** (§ 3): deploy a current build there and restart it (§ 6) - no other client and no server DLL is involved. Then read `Attack bridge: ...` in **that** client's `BepInEx\LogOutput.log`: no line at all = a DLL older than the fix (the bridge never sent anything, § 3); `sent again as <name> (normal attack)` = the creature's basic attack was sent, `... (special attack)` = its heavy one (middle click), `... (block attack)` = the third one was sent for the block key (right click), `... (word match)` = no weapon pool could be read (§ 3); `no creature equivalent (player N, creature M triggers)` = a reference set came back empty (`M = 0`: look just above it for `Prefab not found` or `Inert copy: ... survived the strip`); `skipped: the component is not the local player's` = the bridge never saw your own animator. Ranged weapons and gestures have no creature equivalent and never animated (§ 4.7) |
| Your secondary attack (middle click by default) plays exactly the same animation as your normal attack | The creature's weapon pool holds a single attack (a boar or a wolf really has no special attack) or could not be read: both attack keys play the normal one, by design (§ 3). The `Attack bridge:` line ends with `(normal attack)` in that case, `(special attack)` when a special attack really exists |
| Your block (right click by default) animates nothing on the creature | There is no third attack to designate: the pool has two attacks or fewer (a goblin, a greydwarf, a draugr) - or none readable at all (a boar's pool animation `base_attack` is a chain the controller only carries as `base_attack0..3`). `Attack bridge: block -> ...` in the log says `no third attack (...)`, and ends with `(block attack)` on a creature that really has a middle one (a Troll, a wolf). Left and middle click are unaffected |
| The other maps still show your real position although `disguise setFakePosition` ran | The fake is written **by the server**, into the player list it broadcasts (§ 2): check `disguise status` (is it published?), then the server log for `Map position spoof for ZDO ...`. The dedicated server must run the current DLL (§ 1.b) |
| Crash at startup | Search `Disguise` in `BepInEx\LogOutput.log` on both sides |
| The mod "restarts" by itself | Normal: re-application every `ReassertSeconds` |
| You fly on your own screen but nobody else sees you in the air | The raise is relayed by the **server** (§ 3): the dedicated server must run the current DLL (§ 1.b) - with it, a vanilla client receives your creature already raised; without it, only your own view flies (`disguise fly on` then `disguise status` to confirm the height is published). Two older mistakes produce the same symptom, both fixed by deploying the **current** build on the server and restarting it (§ 6): a server whose DLL predates the flight, or one whose DLL raised the position inside `ZDO.Serialize` - the body of a ZDO, which the receiver never reads for a position |
| The costume does not move or turn for the others | The pose is relayed exactly like the flight (§ 3): the dedicated server must run the current DLL (§ 1.b). `disguise status` must show your values on its `Pose:` line (they reach your ZDO within a tenth of a second; being memory only, a restart puts them back to `0 0 0`). Your own screen moves the clone immediately with `SeeYourDisguise`, which is how to tell a publication problem from a rendering one |
| The climb / descent keys do nothing | The flight must be **on** first (`disguise fly on` or `FlyEnableKey`): `FlyUpKey` / `FlyDownKey` only act while it is. Then check the four shortcuts in the `[5 - Flight]` section - a value BepInEx cannot read leaves that binding empty and logs `Failed to read keybind from settings: ...` in the log, which needs the format `main key + modifiers` (`UpArrow + LeftAlt`, main key first: `LeftAlt+Home` would be read reversed). Re-record it in ConfigurationManager (Alt+F10) instead of typing it. An unset shortcut (`Not set`) never fires, and nothing of it ever throws |
| Your own viking stays on the ground while the creature flies | Normal: your body really is on the ground (§ 4.8). Turn `SeeYourDisguise` on (§ 2) to watch the creature fly on your own screen too |
| The wheel still zooms instead of climbing (or does nothing at all) | The flight must be **on** first (`FlyEnableKey` / `disguise fly on`), `FlyScrollWheel` must be `true`, and `FlyScrollStep` must not be `0` - a step of `0` hands the wheel back to the camera by design. All three are read every frame, so no restart is needed after a change |
| The camera does not zoom while you fly | Normal: while the flight is on, the wheel belongs to the creature (`FlyScrollWheel`). Land (`FlyDisableKey`) or set the option to `false` and the zoom is back immediately - the minimap and the build rotation were never affected |
| A mob attacks the air below you while you hover over it | Normal: the server simulates your player on the ground (§ 4.8) - that is what makes the flight unusable as a cheat. Landing puts both views back in agreement |
| `disguise gui` says "not opened" (or `GuiKey` does nothing at all) | A dedicated server has no GUI, and before a world is loaded Jotunn's `CustomGUIFront` does not exist yet (`Window: ...` in the log, visible only with `Logs = true`, § 2). An unset `GuiKey` (`Not set`, recorded in ConfigurationManager) never fires. Jotunn itself is a hard dependency (§ 1): without it the mod does not load at all |
| You click a row, or type in the filter, and the character moves anyway (or the camera spins behind the window) | The two patches of the window are not running: the DLL in `BepInEx\scripts` is older than the build → deploy a current one and **restart** (§ 6). While it is current, both take a frame only when the pointer is *on* the window **or** the filter field is focused: the window is an overlay, and everything outside of it keeps working on purpose (§ 2) |
| The list is empty and reads "the prefab list is available in game" | Normal in the main menu: there is no `ZNetScene` to list prefabs from yet, exactly like `disguise list`. The window rebuilds its list at every opening, so open it again once a world is loaded. An empty list in a world means the six `Show*` boxes of `6 - GUI` are all off - or that `ShowFavorites` is ticked while `Favorites` is still empty - the window then says "no prefab matches the filter" instead |
| Your building costume does not follow you on the client (it stays where you changed it) | Normal, and it is the prefab and not the sync: on a machine running no DLL of ours the only code that applies a ZDO position to a transform is `ZSyncTransform`, and no `Piece` has one - the client drew your wall once, where your ZDO was, and has nothing left to move it with (§ 3). `disguise set <creature>` follows everywhere; so do ships, carts and dropped items. The `Static` category of the window is precisely the set of prefabs that cannot move, and `disguise list` prints its count (`static <n>`) |
| A cart or a raft costume shivers by a few centimetres on the machine of a player watching you | The ground rest is relayed by the **server** (§ 3): the dedicated server must run the current DLL (§ 1.b) - with it, the costume is sent to the height where it rests instead of one it has to be pushed out of, and the receiving body stops fighting back. Water and thin air never had that shiver (there is no contact to lose), and neither does your own clone (GhostFactory gives it no physics at all). With `Logs = Enabled` the log says what a costume really gets: `Ground rest: <name> is relayed <n> m above its player, laid <d> degrees off upright | ground <x> m vs feet (<ray|probe|heightmap|zone>), rise <r> m` - a lift of roughly *hang + 5 cm* with `rise 0.00` **and a tilt above zero degrees on a slope** is the costume lying on it, while the same lift with `laid 0.0 degrees` on ground you can see sloping, or `no terrain read` when none of the four reads answered, means the costume never left the terrain buried in it; a failed read prints its own line right after it - `Ground rest: no ground under <name> at (x, y, z) \| terrain mask <n> (layer <n>) \| <what the unfiltered ray met> \| heightmaps <n>, Heightmap.GetHeight <resolved\|not resolved> \| zone probe <reason>` - which is the whole diagnosis of that case. `ray meets nothing in 10000 m` together with maps that do not cover your feet is the dedicated server standing on zones it ghost-generated (§ 3), and the reason at the end says how far the zone got: `terrain build timed out` or `another zone is building` means the tile was still being produced when the bounded wait ran out - it only has to be produced once, so the next packet has it -, `reflection unresolved` means this machine cannot ask the game at all (an error saying so is logged once), while `Ground rest: no terrain instance stands where the costume is read, so the zone at (x, z) is built to read it.` (written once) says a zone stands there, and the read that used it reports the source as `zone`. `Ground rest: <name> rotation written into the packet: <d> degrees` is the companion line showing that tilt really went out. |
| A cart or a raft costume looks wrecked on another player's screen (missing planks, a torn sail) | The costume is wearing **your** hit points: `WearNTear` reads the `health` of the ZDO it is attached to, and your ZDO carries yours (§ 3). The relaying server swaps it for the costume's own maximum for the duration of the packet, so the dedicated server must run the current DLL (§ 1.b). Nothing about it changes for you, for the server's own state or for the save file - and the trade-off is that nobody but you and the server can read your health through a disguise any more |
| The log of the player watching you fills with `Failed to find rpc method 213315071` (or `531685242`) | Harmless, and both hashes are known: `213315071` = `RPC_ResetCloth`, broadcast by the vanilla `Character.ResetCloth()` whenever your body starts moving (and on a jump); `531685242` = `SetTrigger`, the animation trigger broadcast (§ 4.7). A client without the DLL builds your costume, and a wall has neither a `Character` nor a `ZSyncAnimation` to answer with - the machine logs that it knows no such method and does nothing, which is exactly right since there is nothing to reset or to animate. A creature costume logs neither (both components are there), and a client running the DLL never does either (it builds a real `Player`) |
| A prefab a mod adds is missing from the list | Normal: both lists only show **vanilla** prefabs - the names come from the game's own asset manifest (`valheim_Data\StreamingAssets\SoftRef\manifest` and `manifest_extended`), which no mod ever writes to (§ 6). `disguise set <name>` still accepts a modded name typed by hand, and a configured modded prefab still works |
| An `fx_` / `sfx_` / `vfx_` prefab is missing from the list | Normal, and vanilla as they are: those prefixes name the **effect** prefabs - the sound and the particles something spawns, not an appearance - and both lists drop them on purpose (§ 6). `disguise set <name>` still takes one typed by hand |
| A prefab like `Abomination_attack1` or `wraith_melee` is missing from the list | Normal, and no accident: those are the creature's own **attack helpers** and zones - registered `Item` prefabs with **no mesh anywhere**, which is why the log says `Preview: <name> has no mesh to show.` when one is pointed at. The catalogue drops them with every other prefab nothing could be drawn of (a spawner, a blob's AoE): they would be an empty picture in the window and an invisible viking in the world (§ 6). `disguise set <name>` still takes one typed by hand |
| The shimmer beside an item costume is still there | `HideItemSparkle` only reaches what **this machine** draws: your own costume (`SeeYourDisguise`, § 2) and the picture of the window - so tick `Hide item sparkle` under the command buttons (or write `HideItemSparkle = true` in the `.cfg`) and both drop it at once, the clone being rebuilt rather than repaired. A machine with no DLL of ours builds your **real item prefab** from the network, where the particle system is baked into the prefab and no setting of ours exists to turn it off; a machine running the DLL draws a real `Player` for you and never your costume (§ 3), so there is nothing of yours there to shimmer |
| A level 2 or 3 costume shows the plain creature | Three places can show it, each with the game's own rule behind it. **Another machine**: a creature reads its level once, when it is born (`Character.Awake`) and keeps that instance forever, so a machine already drawing you needs a fresh object before a new skin can show - `ForceObserverRefresh` (§ 4.1) does it on its own, `disguise refresh` is the manual fallback, and the dedicated server must run the DLL for either. **Your own screen / the picture**: those are copies this mod builds, dressed by `LevelSkin` at their build - the level is read once per build, so a level that just moved rebuilds them within the same frame (the picture after its 0.15 s hover delay). **Everywhere at once**: a level above the skins a prefab carries keeps only the health growing, and the game draws the base look - `spawn <creature> 1 4` in vanilla behaves exactly the same. A star only ever appears at levels **2 and 3** (`EnemyHud` draws it for those two alone) |
| The pause menu has no `Disguise` line | `ShowMenuEntry` is off (`6 - GUI`, on by default). Set it back to `true` and the line comes back at the next opening of the pause menu - at once, if that menu is on screen while ConfigurationManager changes it. The line is there in the main menu too, where the window only says `the prefab list is available in game` until a world is loaded |
| Opening the window takes a noticeable moment | Two things cost, and the log says both: `GUI: <n> name(s) in <ms> ms, <built> of <n> row(s) in <ms> ms.` (`Logs = true`, § 2). The catalogue is the walk that tags every vanilla prefab with its kind - up to ten component tests, and exactly ONCE: the walk skips them when all it was asked for is names, and the window does that one pass for its own rows. The rows are Jotunn buttons of four objects each, and only the categories the boxes have ON get one at the opening; a category switched on later is built then, once (in the order of the list), and a row is never destroyed, so switching back and forth stays a `SetActive`. The very first list of a session also reads the game's vanilla manifest once (3.5 MB, then `Vanilla prefab set: <n> name(s).` in the log) |
| The cursor stays free after the window is closed (or in a later world, the camera never follows the mouse again) | `GameCamera.m_mouseCapture` is still handed to Jotunn: `DisguiseGui.Tick` notices the mismatch on the next frame and releases it (a window destroyed by a scene change cannot release it by itself). If it survives that, deploy a current build and restart (§ 6) |

Log files:

- PC: `C:\Users\a\AppData\Roaming\com.kesomannen.gale\valheim\profiles\dev\BepInEx\LogOutput.log`
- Server: `...\profiles\<server profile>\BepInEx\LogOutput.log`

In the PC log, also look for the Script Engine line (it is what loads the DLL):
`[Info :Script Engine] Loading plugins from ...\BepInEx\scripts\Disguise.dll`
