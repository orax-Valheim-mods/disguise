# PLAN.md — A line in the pause menu, and taking it out again

**Scope.** How to add a line of your own to Valheim's pause menu (the list Escape
draws), and how to remove it when the plugin is unloaded. Written so the same
feature can be implemented in any other BepInEx mod: the recipe is mod-agnostic,
the names in `code` are what **this** repository calls things.

**Reference implementation** — read alongside this document:

| File | What it holds |
|---|---|
| `Disguise/PauseMenuEntry.cs` | the whole feature: state, build, wire, remove |
| `Disguise/Patch_MenuShow.cs` | postfix that keeps the entry present at every open |
| `Disguise/Patch_MenuNavigation.cs` | postfix that keeps the D-pad path reachable |
| `Disguise/GameAccess.cs` (`GetMenuSettingsButton`, `HideMenu`) | the two game members this touches |
| `Disguise/DisguiseConfig.cs` (`ShowMenuEntry`) | the on/off switch |
| `Disguise/Disguise.cs` (`OnDestroy`) | where the entry is released on unload |

---

## 1. What the entry hooks into

| Member of `Menu` | Visibility in the game | Role in this feature | How this mod reaches it |
|---|---|---|---|
| `Show()` | public | opens the pause menu, every time Escape opens it | Harmony **postfix** (`Patch_MenuShow`) |
| `UpdateNavigation()` | **private** | rewrites the gamepad up/down chain over the buttons the menu knows about | Harmony **postfix**, patched **by `nameof`** (`Patch_MenuNavigation`) |
| `Hide()` | public | closes the menu through the game's own hand (unpause + cursor back) | reflection (`GameAccess.HideMenu`) |
| `m_settingsButton` | public field | the Settings line: the shape to clone and the place to stand after | reflection (`GameAccess.GetMenuSettingsButton`) |
| `settings.transform.parent` | — | the object every line of the list is a child of | read from the button itself |

Five facts that the whole feature is built on:

1. **`Show` runs once per opening.** A Harmony postfix on it is therefore the only
   place that needs to (re)create the entry — no polling, no `Update`.
2. **Harmony runs a postfix even when a prefix stopped the method.** The same
   class also holds a prefix that blocks `Show` while another window is open; the
   postfix still fires, inside a menu that is never displayed. That is harmless
   (a null test and a `SetActive`) and is why `Add` is written to be cheap.
3. **`UpdateNavigation` is private and rewrites the chain from the buttons the
   game knows about.** Our button is not one of them, so every call of that method
   erases the two links we added — hence the second postfix.
   Patching a private method costs Harmony nothing (it resolves the target by
   name), and writing the patch **as `nameof(Menu.UpdateNavigation)`** costs the
   compiler nothing either: this project builds against
   `valheim_Data\Managed\publicized_assemblies\*.dll`, where *every* member is
   public, while the game at runtime loads the original assembly. `nameof` is a
   compile-time string constant, so it both compiles and emits **no member
   reference at all** in the DLL — the visibility audit (§ 5) has nothing to see.
4. **Unity layout is driven by sibling index** inside the list's parent: the
   visual place and the D-pad place are two separate things to set.
5. **A `Button` carries the listeners its prefab wired in.** See the trap in § 2.

---

## 2. Adding the entry — the recipe

Work in one class, `internal static`, one file (this repo's rule). The state
below is what every step needs:

```csharp
private static Button?  s_button;        // the clone; null = nothing on screen
private static Menu?    s_menu;          // the menu it lives in (for Hide + reflection)
private static Selectable? s_belowSettings; // what Settings pointed at BEFORE we wired
private static bool     s_failed;        // log a build failure once, never twice
```

**Step 1 — a postfix on `Menu.Show`.** One call, no logic in the patch:

```csharp
[HarmonyPatch(typeof(Menu), nameof(Menu.Show))]
internal static class Patch_MenuShow
{
    private static void Postfix(Menu __instance) => PauseMenuEntry.Add(__instance);
}
```

**Step 2 — `Add(menu)`: read the switch, build once, wire every time.**

```csharp
internal static void Add(Menu menu)
{
    try
    {
        s_menu = menu;
        if (!YourOption.Value) { Forget("option is off"); return; }  // § 3
        if (s_button == null)  { s_button = Build(menu); }           // lazy, once
        if (s_button != null)  { WireNavigation(); }                 // every open
    }
    catch (Exception ex) { /* log ONCE via a s_failed flag */ }
}
```

- **Lazy build**: nothing exists until the first open, and a scene change
  destroys the clone with its parent — Unity's own null test (`== null` is true
  of a destroyed object) then rebuilds it. No explicit scene hooks are needed.
- **Try/catch**: the menu belongs to the game; an exception thrown out of a
  postfix would land in `Menu.Update`. Log it once, not once per frame.

**Step 3 — `Build(menu)`: clone the game's own line.**

```csharp
Button? settings = GameAccess.GetMenuSettingsButton(menu);
Transform? parent = settings.transform.parent;

GameObject entry = Instantiate(settings.gameObject, parent); // same look, same layout
entry.name      = "YourEntry";                               // recognisable in the hierarchy
entry.SetActive(true);                                       // Settings may be hidden (demo mode)

Button button = entry.GetComponent<Button>();
button.onClick = new Button.ButtonClickedEvent();   // <-- REPLACE, never AddListener on top
button.onClick.AddListener(OnClicked);
button.interactable = true;
SetLabel(entry, "Your label");

entry.transform.SetSiblingIndex(settings.transform.GetSiblingIndex() + 1); // right after it
```

Traps, each of which breaks something visible if skipped:

| Step | What goes wrong without it |
|---|---|
| Clone instead of building your own objects | you re-implement the game's styling, spacing and localisation |
| `button.onClick = new ButtonClickedEvent()` | `RemoveAllListeners` does **not** remove listeners the *prefab* wired — your line would open Settings behind whatever you just opened |
| `SetActive(true)` | the clone inherits whatever state Settings was in (hidden in demo mode) |
| `SetSiblingIndex(settings + 1)` | the line lands at the end of the list, while the D-pad chain (step 4) is written for "right after Settings" |
| Set **both** labels (below) | you see the right word, the game's localiser rewrites the other kind back |

`GameAccess.GetMenuSettingsButton(menu)` above is **this repo's** reflection helper for the
public field `Menu.m_settingsButton`; in another mod, read that field however your project
reaches game members (step 8). Same for `GameAccess.HideMenu(menu)` in the click handler: the
game's `Menu.Hide()` is what unpause + cursor are, and calling it yourself instead of
deactivating the menu leaves the game paused behind your window.

**Step 4 — `WireNavigation()`: the D-pad path.** Explicit navigation, and save
what you are about to overwrite, because § 3 has to give it back:

```csharp
internal static void WireNavigation()
{
    if (s_button == null) return;                 // gone (scene change): next Show rebuilds
    Button settings = GameAccess.GetMenuSettingsButton(s_menu);
    if (settings == null || settings.navigation.selectOnDown == s_button)
        return;                                   // nothing to patch, or already wired

    s_belowSettings = settings.navigation.selectOnDown;   // SAVE FIRST

    Navigation mine = s_button.navigation;
    mine.mode        = Navigation.Mode.Explicit;
    mine.selectOnUp   = settings;
    mine.selectOnDown = s_belowSettings;
    s_button.navigation = mine;

    Navigation after = settings.navigation;
    after.selectOnDown = s_button;                // Settings -> you -> what followed Settings
    settings.navigation = after;
}
```

- **Idempotent by construction** (`already wired` test): it is called on every
  open, and again after the game rewires its own chain.
- **Explicit mode**: automatic modes recompute a direction from the scene and
  would ignore your "stand after Settings" intent.

**Step 5 — a second postfix, on `Menu.UpdateNavigation`.** Same shape as step 1,
calling only `WireNavigation()`. Without it the entry is clickable with the mouse
and unreachable with a D-pad.

**Step 6 (optional) — an on/off switch.** Read it inside `Add` (so it is
re-evaluated at every open) **and** bind a `SettingChanged` handler that calls
`Add(s_menu)` for a change applied while the menu is on screen. This repo's entry
is `ShowMenuEntry` in `6 - GUI`, default `true`.

**Step 7 — the label.** Valheim's localiser walks `Text` and `TMP_Text` alike
every thirty frames while the menu is on screen, so set whichever the clone
carries (test both, `GetComponentInChildren<TMP_Text>` / `<Text>`), and keep the
word free of `$` — without it the localiser treats it as literal text and never
translates it. Every label of this mod is English.

**Step 8 — how game members are reached.** The build compiles against publicized
assemblies (every member public) but the game runs the original ones: a direct
reference to a private member therefore **compiles and throws at runtime**
(`FieldAccessException`) — that is exactly what `audit_visibility.ps1` catches
(§ 5). This repository's stricter rule is to reach **every** game member through
its reflection layer (`GameAccess`), public or not, so the DLL's set of direct
member references never grows and never shifts with a game update. In another
mod: private members must be reflected (`BindingFlags` covering
`Public | NonPublic`); public ones may be referenced directly if your project
allows it — and a visibility audit is still worth keeping.

---

## 3. Removing the entry — the unload recipe

### Why a removal is mandatory

BepInEx **ScriptEngine** reloads a mod like this (see `ScriptEngine.ReloadPlugins`):

1. `Object.Destroy(scriptManager)` — the object holding every script plugin; each
   plugin's `OnDestroy()` runs then (end of frame), **before** anything new loads;
2. the DLL is read again into a **new assembly** (its name gets a `-[ticks]`
   suffix), and the plugin is added to a **new** object next frame.

Consequences, and they are the whole point:

- **Statics are fresh.** The copy loaded next has `s_button == null`. It knows
  nothing of the clone the previous copy built.
- **You unload nothing by yourself.** The clone is a child of a **game** object;
  nothing in the game will ever destroy it. It stays on screen, and the next copy
  builds a second one right beside it → the classic "my line appears twice after
  a reload".

So: **anything your mod put into an object the game owns must be released in
`OnDestroy`.**

### The removal, in this order

```csharp
internal static void Cleanup()
{
    try { Forget("plugin unloaded"); }
    catch (Exception ex) { /* the logger still works here: it dies one line later */ }
}

private static void Forget(string why)
{
    Button? button = s_button;
    s_button = null;                       // 1. forget FIRST: re-entrant calls now no-op
    if (button == null) return;            //    nothing on screen: nothing to undo

    Button? settings = GameAccess.GetMenuSettingsButton(s_menu);
    if (settings != null && settings.navigation.selectOnDown == button)
    {
        Navigation after = settings.navigation;
        after.selectOnDown = s_belowSettings;   // 2. give the chain back what you took
        settings.navigation = after;
    }
    Navigation mine = button.navigation;
    mine.selectOnUp = null;                    // 3. drop your own links
    mine.selectOnDown = null;
    button.navigation = mine;

    LogInfo($"... entry removed ({why}).");    // 4. say which reason (option / unload)
    Destroy(button.gameObject);                // 5. destroy the GameObject
    s_belowSettings = null;
}
```

Why each line is where it is:

| Step | Why |
|---|---|
| `s_button = null` first | `Forget` runs from two places (option off, unload); a second entry must find nothing to do |
| restore `settings.selectOnDown` **only if it still points at you** | the game rewires its chain on its own; if it already overwrote your link there is nothing to restore — writing yours back would break the game's chain |
| clear your own links | leaving them is harmless for the game, but they would be a stale reference in a button about to die |
| `Destroy(...)` — **never `SetActive(false)`** | an **explicit** `selectOnDown` is followed whether the target is active or alive: a hidden-but-still-wired entry makes the D-pad go nowhere, and the next copy would still build a second one |
| keep `s_menu` (do not null it) | the switch must be able to add the line again while the menu is open |

### Where it is called

In the plugin's `OnDestroy`, **before `s_instance = null`** (the log helpers go
with the instance — a cleanup called after it logs nothing):

```csharp
public void OnDestroy()
{
    DisguiseGui.Shutdown();        // your window, the mouse it captured, its preview stage
    SelfView.Shutdown();           // any clone you placed in the scene
    PauseMenuEntry.Cleanup();      // THIS document's feature
    PatchHarmony?.UnpatchSelf();   // every Harmony patch you applied
    PatchHarmony = null;
    s_instance = null;             // last
}
```

### The unload checklist (fill the middle column for YOUR mod)

| Thing a mod can leave behind | In this mod | Self-heals? |
|---|---|---|
| A line in the pause menu | `PauseMenuEntry.Cleanup()` | **No — this is the one that duplicates** |
| Harmony patches | `PatchHarmony.UnpatchSelf()` (one instance, owner id = plugin GUID) | No: patches of the old copy would run forever |
| A window / any Jotunn GUI + the mouse | `DisguiseGui.Shutdown()` → destroy + `GUIManager.BlockInput(false)` | No |
| Clones you instantiated (your own preview, a local body) | `SelfView.Shutdown()`, `CreaturePreview.Forget()` | No |
| A console command in Valheim's `Terminal` | nothing needed | **Yes**: `new Terminal.ConsoleCommand(...)` executes `commands[name] = this`, so the new copy overwrites the entry (an `Add` would have thrown) |
| The costume / any object bound to a ZDO | nothing needed | Yes: it is created by the game's own object path and belongs to its ZDO, not to your instance (this is what lets a save survive the mod's removal) |
| Statics, config entries | nothing needed | Yes: a new assembly has fresh statics; the new copy rebinds the same `.cfg` file |
| Coroutines, `Update` | nothing needed | Yes: they die with the component being destroyed |

---

## 4. Porting this to another mod

1. Copy the shape of `PauseMenuEntry.cs` (one `internal static` class, the four
   static fields, `Add` / `Build` / `WireNavigation` / `Forget` / `Cleanup`).
2. Replace the label, the click handler, and the option name. Keep: the clone,
   the replaced `onClick`, `SetActive(true)`, the sibling index, the saved
   `s_belowSettings`, both label kinds.
3. Add the two Harmony postfixes (`Menu.Show`, `Menu.UpdateNavigation`). Keep the
   `nameof` form for the private one.
4. Call `Cleanup()` from **your** plugin's `OnDestroy`, before whatever your
   logger needs, and audit your own state against the checklist in § 3 — your mod
   will own things this one does not (a HUD element, a particle, a registered
   event, an `Update` loop).
5. Do the verification of § 5, in particular the reload test: a duplicated line
   is exactly the symptom of a missed cleanup.

---

## 5. Verification

Run from the repository root:

```powershell
csharpier format .
dotnet build Disguise.sln -v q -nologo      # 0 error, 0 warning
powershell -ExecutionPolicy Bypass -File .\audit_visibility.ps1
#   -> "Game members referenced: <n>   ISSUES: 0", exit code 0
```

The audit re-reads the compiled DLL with Mono.Cecil, keeps every member reference
whose declaring type lives in `assembly_valheim` / `assembly_utils` (that number
is `Game members referenced: <n>`), resolves each of them in the **original** game
assembly, and reports the ones that are not public — those compile fine against
the publicized build-time assembly and throw at runtime. Expected output:
`Game members referenced: <n>   ISSUES: 0`, exit code 0 (`ISSUES` above 0 exits
with 2). `<n>` grows when you add a new game member; that is normal, `ISSUES: 0`
is what must hold.

In game:

1. `Escape` → your line is there, once, right under `Settings`;
2. click it → the pause menu closes and your window opens (no Settings opening
   behind it);
3. with a gamepad, `Down` from `Settings` reaches your line;
4. turn your option off → the line disappears at once; on → it comes back;
5. reload the mod — `F6` (ScriptEngine's `ReloadKey` default) or, when its file
   watcher is on (this dev profile sets `EnableFileSystemWatcher = true`), simply
   letting the build copy `Disguise.dll` into `BepInEx\scripts` and waiting the
   3 s of `AutoReloadDelay` → the log shows `Pause menu: entry removed (plugin
   unloaded).` then `Pause menu: entry added (Disguise).`, and `Escape` still
   shows **one** line. Reload a second time to be sure it is stable.

---

## 6. Conventions of this repository

- `README.md` is the user documentation (long, and the single source of truth for
  the options and the troubleshooting); `PLAN.md` — this file — is the developer
  note for one feature and is not linked from the README;
- one `internal static` class per file; `#nullable enable` at the top of a new file;
- GUI strings in English;
- version `0.1.0`, `PluginGUID = orax.Disguise`;
- never run `git push` (the author pushes), never edit `Disguise/Package/README.md`
  (regenerated by `scripts/publish.ps1`); the hand-written doc is `README.md`;
- commits are written to a file first and committed with `git commit -F <file>`
  (no shell heredoc).
