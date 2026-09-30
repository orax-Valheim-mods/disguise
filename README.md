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

> ⚠️ Without the server DLL, run **`disguise disable` before stopping / saving the server**
> (§ 5): `ProtectSaveFile` does not exist there, so the creature would be written to the
> world save.

Copy the same DLL into:

```
C:\Users\a\AppData\Roaming\r2modmanPlus-local\Valheim\profiles\serveur\BepInEx\plugins\_manual_install\
```

This folder is created automatically by the build (see § 6).

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
| `disguise list` | List of usable creature prefabs (`disguise list troll` to filter) |
| `disguise set Troll` | Change creature and enable the disguise |
| `disguise enable` | Enable the disguise |
| `disguise disable` | Disable: you become a viking visible to everyone again |
| `disguise refresh` | Force every loaded player to re-create your object **now** (manual fallback - needs the DLL on the server, § 1.b) |
| `disguise help` | List of the available commands (`disguise ?` also works) |

The console tab-completion suggests the sub-commands.

### Configuration (`.cfg` file)

File: `...\profiles\<profile>\BepInEx\config\orax.Disguise.cfg`

| Option | Default | Role |
|---|---|---|
| `Enabled` | `true` | Disguise on/off |
| `CreaturePrefab` | `Troll` | Exact name of the creature prefab |
| `ProtectSaveFile` | `true` | Writes `Player` (not the creature) to the world save - **needs the DLL on the server**, since the server writes the save (§ 1.b) |
| `ReassertSeconds` | `2` | Re-application period on your player |
| `ForceObserverRefresh` | `true` | Automatically make other players re-create your object after a change (§ 4.1) |
| `RefreshHoldSeconds` | `0.6` | Delay (seconds) before other players see the new look; increase on a laggy connection |

**Display section** (`3 - Display`, all `false` by default) - what the **other machines**
see while you are disguised. `VisEquipment` reads the equipment hashes from your ZDO and
attaches those items to the bones of whatever prefab carries it: on a vanilla client that
prefab is your **creature**, so a helmet / cape / leggings gets glued at a nonsense
position on humanoid creatures. These options hide the concerned keys **for the other
machines only** - your own view always keeps your equipment.

| Option | Default | Role |
|---|---|---|
| `HideHelmet` | `false` | Hide the helmet from the other machines |
| `HideChest` | `false` | Hide the chest armour |
| `HideLegs` | `false` | Hide the leggings |
| `HideShoulder` | `false` | Hide the cape / shoulder item |
| `HideHeldItems` | `false` | Hide the items in your hands and on your back |

How it works (`EquipmentMask`): the real hashes always stay in your ZDO, they are only
**swapped out of the packet** while `ZDO.Serialize` runs (the vanilla key carries `0`, the
real hash moves into our own `cd_eq_*` key). The world save reads a *snapshot* taken at the
start of the save (`ZDO.Save`), and the real hashes are written into that snapshot so the
file keeps your equipment; a Harmony patch on `VisEquipment.UpdateEquipmentVisuals` puts
them back for the modded machines, which therefore keep rendering your gear. Nothing is
ever persisted masked and no extra traffic is produced (the swap does not bump the ZDO
revision). `Hide*` changes are re-sent immediately while you are disguised.

These options can also be changed in game with `disguise enable` / `disable` / `set`.

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
   The result: other players see the new look **~0.6 s** after `disguise set/enable/
   disable`, without moving and without anyone reconnecting.
   > This step runs **entirely on the server** (`ZDOMan.SendZDOs`): it needs the DLL
   > installed there (§ 1.b). Without it, everything else still works.

Visual synchronisation on vanilla clients:

- **Position**: yes, through `ZSyncTransform` (the creature follows you everywhere).
- **Animations**: partial. `ZSyncAnimation` synchronises a list of parameters specific to
  each prefab; the viking and creature parameter names only partly match. Common result:
  the creature moves but stays in an idle pose.
- **Name above the head**: no, vanilla clients have no `Player` component for you (the
  server player list does display your name).

---

## 4. Known limitations (normal behaviour, not bugs)

1. **A vanilla client that already saw you** used to keep showing your old look: the game
   never re-creates an object when its prefab changes. The automatic *observer refresh*
   (§ 3, step 5) fixes that: the change becomes visible to everyone
   loaded after ~`RefreshHoldSeconds` (0.6 s), with nobody moving and nobody reconnecting.
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

---

## 5. Clean uninstall (IMPORTANT)

**Never simply delete the DLL while `Enabled = true`.**

Procedure:

1. On your PC: `disguise disable` in the F5 console (your player becomes `Player` in the
   ZDO again, the marker is removed).
2. Let the server run one save cycle (or restart the server).
3. Check with `disguise status` that the prefab is `Player`.
4. Only then, remove the DLL from every location where you installed it (§ 1).

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

- `Disguise.cs` - the plugin class: constants (`PluginGUID`, `cd_disguise`, `cd_refresh`),
  the log helpers and the lifecycle (`Awake` / `OnDestroy` / `Update`), which wires the
  other classes together. Everything it does not share stays `private`.
- `DisguiseConfig.cs` - the eleven settings and their binding to `orax.Disguise.cfg`.
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
- `ObserverRefresh.cs` - the `cd_refresh` window and the fake far-away position the
  modded server relays during it.
- `DisguiseCommand.cs` - the `disguise` console command and its sub-commands.
- `Patch_CreateObject.cs`, `Patch_ZDOLoad.cs`, `Patch_PlayerOnSpawned.cs`,
  `Patch_ZDOSave.cs`, `Patch_ZDODeserialize.cs`, `Patch_SendZDOs.cs`,
  `Patch_CreateDestroyObjects.cs`, `Patch_FindSectorObjects.cs`,
  `Patch_ZDOSerialize.cs`, `Patch_ZDOSaveEquipment.cs`, `Patch_VisEquipment.cs` - the
  eleven Harmony patches, one `internal static` top-level class each. They reach the
  features through the `internal` members of the classes above (fields they swap in/out
  during `SendZDOs`, helpers such as `HasMarker` / `ApplyTo` / `InstantiateAs`) and log
  through `Disguise.LogError(...)` - `s_instance` itself never leaves the plugin class.
  `Patch_CreateDestroyObjects` additionally owns the `s_inCreateDestroy` scope flag read
  by `Patch_FindSectorObjects`.

(to be read together with the game decompilation in `T:\temp\opencode\vh_dec`).

### Visibility audit (re-run after every build)

The project compiles against `valheim_Data\Managed\publicized_assemblies\*.dll`, where
**every** member is made public. The game loads the *original* assembly instead, where
`private` / `protected` stay: a direct reference to such a member makes the plugin fail
at runtime with `FieldAccessException`. Already hit with `Terminal.commands` (protected),
`ZDO.m_prefab` (private), `ZDO.IncreaseDataRevision()` (private), `Character.m_nview`
(protected) and `VisEquipment.m_nview` (private) - all five are now reached through
reflection (the `s_*` fields in `GameAccess.cs`).

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File "A:\_MODDING\Valheim\MODS\Disguise\audit_visibility.ps1"
```

Expected output: `Game members referenced: 45   ISSUES: 0` (exit code 0).
On exit code 2, replace every listed member with reflection
(`FieldInfo` / `MethodInfo` / `AccessTools`).
Note: `ZDOMan.ZDOPeer` (nested type of the game) is **not public**, so the mod never
mentions it in a signature - Harmony patch targets are only strings (`nameof`), and the
outgoing packet is patched through `ZDOMan.SendZDOs` without any of its parameters.

---

## 7. Troubleshooting

| Symptom | Check |
|---|---|
| `SymbolsNotFoundException: No symbol found for file ...` | `Disguise.pdb` missing next to the DLL in `BepInEx\scripts\` → rebuild (§ 6) |
| `FieldAccessException: Field ... is inaccessible from method ...` | A game member is `private`/`protected`: never access it directly, use reflection, then `audit_visibility.ps1` (§ 6) |
| Nothing changes for the others | `disguise status` (wrong prefab?), then look for `Loading [Disguise 0.1.0]` in the **PC** log. If they only miss the *change* and not you: no refresh without the server DLL (§ 4.1) |
| `disguise status` says "NOT FOUND" | Wrong prefab name → `disguise list` |
| You appear as a viking to others | They already saw you before → run `disguise refresh`, or move ~100 m / reconnect (§ 4.1) |
| **You are invisible to a vanilla client** | Only possible with the DLL on the server: the relay is stuck on the fake position (the owner never sent `cd_refresh = 0`): the server log must show `Observer refresh flag cleared on ZDO ...` after every change. Restart the dedicated server (the flag is stripped at world load) with the current build. |
| Others see your new look too late | Normal: ~`RefreshHoldSeconds` (0.6 s); raise it in the `.cfg` on a laggy connection |
| Fake position warning in the server log | `Observer refresh flag still set after 5 s`: owner vanished mid-refresh; it stopped by itself |
| Crash at startup | Search `Disguise` in `BepInEx\LogOutput.log` on both sides |
| The mod "restarts" by itself | Normal: re-application every `ReassertSeconds` |

Log files:

- PC: `C:\Users\a\AppData\Roaming\com.kesomannen.gale\valheim\profiles\dev\BepInEx\LogOutput.log`
- Server: `...\profiles\<server profile>\BepInEx\LogOutput.log`

In the PC log, also look for the Script Engine line (it is what loads the DLL):
`[Info :Script Engine] Loading plugins from ...\BepInEx\scripts\Disguise.dll`
