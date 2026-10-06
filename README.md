# Disguise

Makes **your player appear as a creature** to every other player, **including players who have no mod installed**.

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
| `disguise status` | Current state (creature, marker, save protection) |
| `disguise list` | List of usable creature prefabs (`disguise list troll` to filter; the window lists the same creatures, § 2 GUI) |
| `disguise set Troll` | Change creature and enable the disguise (a click on a row of the window does exactly this, § 2 GUI) |
| `disguise enable` | Enable the disguise |
| `disguise disable` | Disable: you become a viking visible to everyone again |
| `disguise refresh` | Force every loaded player to re-create your object **now** (manual fallback - needs the DLL on the server, § 1.b) |
| `disguise view [on|off]` | Show the creature on **your own screen** instead of your viking (toggles without an argument, purely local) |
| `disguise logs [on|off]` | Write the mod's own **Info** messages to the BepInEx log (toggles without an argument; warnings and errors are always written, § 2) |
| `disguise fly [on|off]` | **Raise the creature above you** for the other machines (toggles without an argument, § 2). Purely visual: your player never leaves the ground |
| `disguise setFakePosition [x z \| off]` | Show a **fake position** on the other players' map: no argument records where you stand, `off` shows the real one again (needs the DLL on the server, § 1.b) |
| `disguise gui [on|off]` | Open / close the Disguise window (`GuiKey`, § 2): same thing as the key chord, for whoever prefers the console |
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
| `CreaturePrefab` | `Troll` | Exact name of the creature prefab |
| `ProtectSaveFile` | `true` | Writes `Player` (not the creature) to the world save - **needs the DLL on the server**, since the server writes the save (§ 1.b) |
| `ReassertSeconds` | `2` | Re-application period on your player |
| `Logs` | `true` | Write the mod's own **Info/Debug** messages to the BepInEx log; warnings and errors are always written (`disguise logs [on|off]`, § 7) |
| `ForceObserverRefresh` | `true` | Automatically make other players re-create your object after a change (§ 4.1) |
| `RefreshHoldSeconds` | `1.5` | Delay (seconds) before other players see the new look; increase on a laggy connection |
| `SeeYourDisguise` | `false` | Show the creature on **your own screen** (`4 - Own view`, local only) |

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

**Own view section** (`4 - Own view`, `SeeYourDisguise`, `false` by default) - what **you**
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
leaves the ground (§ 3, and § 4.8 for what it means in game). `FlyEnableKey` starts it (the
creature is raised by 2 m straight away), `FlyUpKey` / `FlyDownKey` climb and descend
**while held**, at `FlySpeed` metres per second, and `FlyDisableKey` lands it. The same two
states are reached from the console with `disguise fly [on|off]`, and `disguise status`
reports the current height.

The **mouse wheel** climbs and descends too while the flight is on (`FlyScrollWheel`, one
notch = `FlyScrollStep` metres), and the camera then waits: the wheel belongs to the creature
alone, so it never climbs *and* zooms at once - the zoom comes back the moment the flight
ends, and the minimap and the build rotation keep their own wheel throughout.

| Option | Default | Role |
|---|---|---|
| `FlyEnableKey` | `Home + LeftAlt` | Start the flight: the creature is raised for everyone who renders you |
| `FlyDisableKey` | `End + LeftAlt` | Land it: back on the ground |
| `FlyUpKey` | `UpArrow + LeftAlt` | Climb while held (only once the flight is on), up to 100 m |
| `FlyDownKey` | `DownArrow + LeftAlt` | Descend while held, down to the ground and never below it |
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

They default to `LeftAlt` on purpose - never `AltGr`, which is the RightAlt of a French
keyboard and types characters all day. Every key of a shortcut must be held at once, in
whatever order, and nothing else is required: the chords are read directly instead of through
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
controls (Jotunn is a hard dependency of this mod, § 1). `GuiKey` opens and closes it
(toggles), and `disguise gui [on|off]` does exactly the same from the console. The window
shows the creature list, the 3D preview and the command buttons - this branch added them one
step at a time, and this step brings the **command buttons** plus the **status line**: a row
of four buttons under the picture, each one running the sub-command its label stands for
exactly as the console would (`Enable` / `Disable`, `Refresh`, `Own view`, `Flight` =
`disguise enable` / `disable`, `refresh`, `view`, `fly`), and reporting its answer on the
line at the bottom of the window. The first button is a toggle: it reads "Disable" while the
disguise is on and "Enable" while it is off, and both that label and the line under the title
are rewritten after every click. The status line holds the hint about clicking a row while no
button has been pressed, and then the **first line** of the answer of the last one (it stays
until the next button, and the hint comes back with the next window): a command can print
several lines, the console keeps them all, and the first one is the headline - the notes a
command goes on printing after it stay in the console. The **creature list**: a filter field
over a scrollable list of every usable creature (the exact list `disguise list` prints), the
row of the creature configured right now highlighted in orange, and a click on any row =
`disguise set <prefab_name>` (config written and applied at once, the line under the title
updated). The rows read beige and the configured one orange - Jotunn paints the text of all
of its buttons orange, so the list itself is tinted beige for the difference to be visible -
and the wheel moves the list by `GuiScrollStep` pixels per notch (3000 by default = about 88
rows of the 153 the list scrolls through; uGUI's own default is `1`). The list is **clamped
at both ends**: an extra notch at the top or the bottom simply does nothing, instead of the
elastic dip-and-spring-back Unity gives by default. The **3D preview** above the buttons
shows the creature of the row the pointer is on, the configured one while the pointer is on
no row at all.

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
the framing measured once from the bounds stays true. Each film holds the world's fog and
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
| `GuiKey` | `F8 + LeftAlt` | Open / close the window (a BepInEx shortcut like the flight keys, recorded in ConfigurationManager, section `6 - GUI`) |
| `GuiScrollStep` | `3000` | Pixels the creature list moves per mouse wheel notch (section `6 - GUI`). Read every frame while the window is open, so a change takes effect at once; `0` holds the list still and a negative value inverts the wheel. uGUI defaults to `1` - one pixel per notch, which is what makes an unconfigured list crawl |

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
   place. It does not know it is a player: no mod required on its side.
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
   disable`, without moving and without anyone reconnecting.
   > This step runs **entirely on the server** (`ZDOMan.SendZDOs`): it needs the DLL
   > installed there (§ 1.b). Without it, everything else still works.

Visual synchronisation on vanilla clients:

- **Position**: yes, through `ZSyncTransform` (the creature follows you everywhere).
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
  `OnDestroy` / `Update`), which wires the other classes together. Everything it does not
  share stays `private`.
- `DisguiseConfig.cs` - the twenty-three settings and their binding to `orax.Disguise.cfg`.
- `GameAccess.cs` - the only class reaching non-public game members: the reflection
  handles (its `s_*` fields) plus the helpers built on them (`GetNView`,
  `GetLocalPlayerZDO`, `GetZDO`, `BumpDataRevision`, `SetZdoIntSilent`).
- `PrefabResolver.cs` - every hash the mod uses (`cd_disguise` / `cd_refresh`, the vanilla
  `Player` prefab hash, the configured creature hash) and `PrefabName`.
- `DisguiseApplier.cs` - `HasMarker` / `ApplyTo` / `InstantiateAs`: writes the marker and
  swaps the network prefab.
- `EquipmentMask.cs` - the equipment display options (`3 - Display`): swaps the equipment
  hashes out of the outgoing packet, out of the save file, and back into the frame the
  modded machines render (§ 2).
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
  the two windows: the outgoing packet (`BeginSend` / `EndSend`, `Patch_SendZDOs`) and the
  camera zoom (`SuspendZoom` / `ResumeZoom`, `Patch_UpdateCamera`).
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
- `DisguiseCommand.cs` - the `disguise` console command and its sub-commands, plus the two
  things the window needs from it so the console and the window can never disagree:
  `CreatureNames(filter)` (the source of `disguise list` *and* of the window's list) and
  `ApplyCreature(name, reason)` (config + re-application, printing nothing: each side
  reports its own way on top of it). `Execute` and `Say` both take the `Terminal` behind the
  command - the one the command was typed in for the console, `null` for a button of the
  window - and `Say` with no terminal writes the line to `GuiStatus` instead: same text,
  only the place where it is read differs. `Run(sub)` is what a button calls.
- `DisguiseGui.cs` - the Disguise window (`GuiKey`, `disguise gui`, § 2): the chord read once
  per frame, the single window that exists at a time, the `BlocksInput` flag both input
  patches read (pointer on the window **or** filter field focused), the handover of the mouse
  through Jotunn's `GUIManager.BlockInput` (balanced
  one `true` per `false`, only inside the `main` scene) and the healing of a window destroyed
  by a scene change, which cannot release that request by itself.
- `GuiWindow.cs` - what goes INSIDE the window and where: Jotunn's wood panel (draggable), the
  centred labels, the title, the state line (rewritten by `RefreshState` after a change), the
  row of four command buttons under the picture (each one runs its sub-command through
  `DisguiseCommand.Run`, then `RefreshState` rewrites the state line and the toggle's own
  label), the status line below them, and the coordinates of the list block. It only builds,
  it never keeps the window alive nor talks to the console - both stay in `DisguiseGui`.
- `GuiCreatureList.cs` - the creature list: Jotunn's filter field and scroll view, one row per
  usable creature built **once** at open (the filter only shows and hides rows with
  `SetActive`, so typing creates nothing), the orange highlight of the configured creature, a
  row click routed to `DisguiseCommand.ApplyCreature`, and `FilterFocused` - the half of
  `BlocksInput` that hands the frame to the UI while the player types. Each row is given the
  listener of the preview (`GuiPreview.WatchRow`), which is how the picture knows what the
  pointer is on.
- `GuiRowHover.cs` - the listener on every row: pointer in, show its creature; pointer out,
  back to the configured one. Two interfaces and no other, on purpose. Unity's `EventTrigger`
  does the same job with two entries, but it also implements scrolling and dragging - and the
  event system only walks up to the FIRST object of a hierarchy that handles an event. An
  `EventTrigger` on a row therefore swallowed the wheel and the dragging of the list (the walk
  stopped on the row, the `ScrollRect` above it never heard of either, and only the scrollbar
  - which no row stands over - kept working), while an object without the interface is simply
  passed over.
- `GuiStatus.cs` - the line at the bottom of the window: the hint about clicking a row while
  no button has been pressed, and the answer of the last command button after that. One line
  holds one message, so `DisguiseCommand.Run` clears it before running and only the first
  line written after that (the headline) is kept; the console, which has a terminal behind
  it, keeps every line.
- `GuiPreview.cs` - the right half of the window: the `RawImage` the preview is drawn into
  (never a hit target, hidden while there is no texture - a `RawImage` with no texture draws
  a white plate) and the rule that decides what is in it: the row under the pointer, the
  configured creature while the pointer is on no row, the change held back 0.15 s so that a
  pointer sweeping the list builds one copy instead of one per row crossed.
- `CreaturePreview.cs` - the stage the preview is filmed from: the free layer, the camera of
  its own and its `RenderTexture`, the two lights, the inert copy of the creature
  (`GhostFactory.BuildInert`), the framing measured from the bounds of its meshes, the
  turntable, and `Forget` - texture released, layer given back to the cameras and the lights -
  called with the window closing.
- `Patch_CreateObject.cs`, `Patch_ZDOLoad.cs`, `Patch_PlayerOnSpawned.cs`,
  `Patch_ZDOSave.cs`, `Patch_ZDODeserialize.cs`, `Patch_SendZDOs.cs`,
  `Patch_CreateDestroyObjects.cs`, `Patch_FindSectorObjects.cs`,
  `Patch_ZDOSerialize.cs`, `Patch_ZDOSerializeRelation.cs`, `Patch_ZDOSaveEquipment.cs`,
  `Patch_VisEquipment.cs`, `Patch_FootStepFindBestStepEffect.cs`, `Patch_SetTrigger.cs`,
  `Patch_SetBool.cs`, `Patch_ZDOGetPosition.cs`,
  `Patch_RPC_SetTrigger.cs`, `Patch_FootStepRPCStep.cs`, `Patch_WritePlayerInfo.cs`,
  `Patch_UpdateCamera.cs`, `Patch_GetMouseScrollWheel.cs`, `Patch_TakeInput.cs`,
  `Patch_SetMouseLook.cs` - the
  twenty-three Harmony patches, one
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

Expected output: `Game members referenced: 82   ISSUES: 0` (exit code 0).
On exit code 2, replace every listed member with reflection
(`FieldInfo` / `MethodInfo` / `AccessTools`).
Note: `ZDOMan.ZDOPeer` (nested type of the game) is **not public**, so the mod never
mentions it in a signature - Harmony patch targets are only strings (`nameof`), and the
outgoing packet is patched through `ZDOMan.SendZDOs` without any of its parameters.
`Humanoid.s_blocking` (private) is not used either: `Patch_SetBool` compares the parameter
against `Animator.StringToHash("blocking")`, the very expression the game uses to define
that constant, so the block key adds no game member at all: still 82, exactly as before it
existed. The flight is written the same way: `cd_fly` travels as an **int** (the `ZDO.Set` /
`GetInt` overloads the mod already used - `ZDO.Set` has no float overload it could reach
for free), `position` is swapped through the very `FieldInfo` of `Patch_SendZDOs`, and
`ZNet.IsServer()` was already referenced - so it adds no game member either, and the count
stays 82.

---

## 7. Troubleshooting

> **Before reading any `Info` line quoted below** (`Attack bridge: ...`,
> `Observer refresh ...`, `Map position spoof ...`, `Local player (ZDO ...)`, ...): those are
> only written while **`Logs = true`** (default, § 2). Run `disguise logs on` first - or set
> it in the `.cfg`. Warnings and errors are written whatever that value is.

| Symptom | Check |
|---|---|
| `SymbolsNotFoundException: No symbol found for file ...` | `Disguise.pdb` missing next to the DLL in `BepInEx\scripts\` → rebuild (§ 6) |
| `FieldAccessException: Field ... is inaccessible from method ...` | A game member is `private`/`protected`: never access it directly, use reflection, then `audit_visibility.ps1` (§ 6) |
| Nothing changes for the others | `disguise status` (wrong prefab?), then look for `Loading [Disguise 0.1.0]` in the **PC** log. If they only miss the *change* and not you: no refresh without the server DLL (§ 4.1) |
| `disguise status` says "NOT FOUND" | Wrong prefab name → `disguise list` |
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
| The climb / descent keys do nothing | The flight must be **on** first (`disguise fly on` or `FlyEnableKey`): `FlyUpKey` / `FlyDownKey` only act while it is. Then check the four shortcuts in the `[5 - Flight]` section - a value BepInEx cannot read leaves that binding empty and logs `Failed to read keybind from settings: ...` in the log, which needs the format `main key + modifiers` (`UpArrow + LeftAlt`, main key first: `LeftAlt+Home` would be read reversed). Re-record it in ConfigurationManager (Alt+F10) instead of typing it. An unset shortcut (`Not set`) never fires, and nothing of it ever throws |
| Your own viking stays on the ground while the creature flies | Normal: your body really is on the ground (§ 4.8). Turn `SeeYourDisguise` on (§ 2) to watch the creature fly on your own screen too |
| The wheel still zooms instead of climbing (or does nothing at all) | The flight must be **on** first (`FlyEnableKey` / `disguise fly on`), `FlyScrollWheel` must be `true`, and `FlyScrollStep` must not be `0` - a step of `0` hands the wheel back to the camera by design. All three are read every frame, so no restart is needed after a change |
| The camera does not zoom while you fly | Normal: while the flight is on, the wheel belongs to the creature (`FlyScrollWheel`). Land (`FlyDisableKey`) or set the option to `false` and the zoom is back immediately - the minimap and the build rotation were never affected |
| A mob attacks the air below you while you hover over it | Normal: the server simulates your player on the ground (§ 4.8) - that is what makes the flight unusable as a cheat. Landing puts both views back in agreement |
| `disguise gui` says "not opened" (or `GuiKey` does nothing at all) | A dedicated server has no GUI, and before a world is loaded Jotunn's `CustomGUIFront` does not exist yet (`Window: ...` in the log, visible only with `Logs = true`, § 2). An unset `GuiKey` (`Not set`, recorded in ConfigurationManager) never fires. Jotunn itself is a hard dependency (§ 1): without it the mod does not load at all |
| You click a row, or type in the filter, and the character moves anyway (or the camera spins behind the window) | The two patches of the window are not running: the DLL in `BepInEx\scripts` is older than the build → deploy a current one and **restart** (§ 6). While it is current, both take a frame only when the pointer is *on* the window **or** the filter field is focused: the window is an overlay, and everything outside of it keeps working on purpose (§ 2) |
| The creature list is empty and reads "the creature list is available in game" | Normal in the main menu: there is no `ZNetScene` to list prefabs from yet, exactly like `disguise list`. The window rebuilds its list at every opening, so open it again once a world is loaded |
| The cursor stays free after the window is closed (or in a later world, the camera never follows the mouse again) | `GameCamera.m_mouseCapture` is still handed to Jotunn: `DisguiseGui.Tick` notices the mismatch on the next frame and releases it (a window destroyed by a scene change cannot release it by itself). If it survives that, deploy a current build and restart (§ 6) |

Log files:

- PC: `C:\Users\a\AppData\Roaming\com.kesomannen.gale\valheim\profiles\dev\BepInEx\LogOutput.log`
- Server: `...\profiles\<server profile>\BepInEx\LogOutput.log`

In the PC log, also look for the Script Engine line (it is what loads the DLL):
`[Info :Script Engine] Loading plugins from ...\BepInEx\scripts\Disguise.dll`
