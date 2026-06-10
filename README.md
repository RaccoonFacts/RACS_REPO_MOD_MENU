# Raccoon Mod Menu
A BepInEx mod for R.E.P.O. with a toggle menu for god mode, speed hacks, noclip, item spawning, cosmetic crates, and player morphing.

> Built for personal/private use with friends and family. Not intended for use in public lobbies.

---

## Features

- **God Mode** — disables all incoming damage
- **Speed Hack** — increased move and sprint speed with infinite stamina
- **Noclip** — fly through walls with WASD + Q/E, Shift to go faster
- **Add $10,000** — adds currency to your run
- **Full Heal** — instantly restores health
- **Spawn from Catalog** — spawn any item registered in the current run
- **Cosmetic Crates** — spawn Common, Uncommon, Rare, or Ultra-Rare crates on demand
- **Level Items** — see all valuables currently in the level, teleport them to you or yourself to them
- **Morph** — disguise yourself as any item currently in the level (hides player model, shadow, and flashlight)

---

## Controls

| Key | Action |
|-----|--------|
| `F8` | Toggle menu open/close |
| `W/A/S/D` | Move while in noclip |
| `E` | Fly up (noclip) |
| `Q` | Fly down (noclip) |
| `Shift` | Speed boost (noclip) |

---

## Requirements

- [BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases) for R.E.P.O.
- R.E.P.O. (Steam)

---

## Installation

1. Install **BepInEx 5.x** into your R.E.P.O. game folder if you haven't already
   - Download the x64 zip from the BepInEx releases page
   - Extract so that `winhttp.dll` and the `BepInEx/` folder sit next to `REPO.exe`
   - Launch the game once to let BepInEx generate its folder structure, then close it

2. Download the latest `REPOMENU.dll` from [Releases](../../releases)

3. Drop `REPOMENU.dll` into:
   ```
   Steam\steamapps\common\R.E.P.O\BepInEx\plugins\
   ```

4. Launch the game. Press `F8` in-game to open the menu.

---

## Building from Source

### Prerequisites
- Visual Studio 2022 or Rider
- .NET Framework 4.7.2
- R.E.P.O. installed via Steam
- BepInEx 5.x installed in the game folder

### Setup
1. Clone the repo:
   ```
   git clone https://github.com/RaccoonFacts/RACS_REPO_MOD_MENU.git
   cd RACS_REPO_MOD_MENU
   ```

2. Open `RaccoonModMenu.csproj` and verify the `GameDir` path points to your R.E.P.O. install:
   ```xml
   <GameDir>C:\Program Files (x86)\Steam\steamapps\common\R.E.P.O</GameDir>
   ```

3. Restore references — the project references these dlls from the game/BepInEx:
   - `Assembly-CSharp.dll` — game classes
   - `UnityEngine.dll` + modules — Unity core
   - `BepInEx.dll` — plugin base
   - `0Harmony.dll` — patching
   - `Photon3Unity3D.dll` / `PhotonUnityNetworking.dll` — multiplayer

### Build
```
dotnet build -c Release
```
Or in Visual Studio: **Build → Build Solution** with the configuration set to `Release`.

Output will be at:
```
bin\Release\net472\RaccoonModMenu.dll
```

### Creating a Release
1. Build in `Release` mode
2. Take the output `RaccoonModMenu.dll`
3. On GitHub: go to **Releases → Draft a new release**
4. Tag it (e.g. `v1.0.0`), write patch notes, attach the `.dll` file
5. Publish

---

## Project Structure

```
RaccoonModMenu/
├── Plugin.cs          # BepInEx entry point + all mod logic
├── RaccoonModMenu.csproj
└── README.md
```

---

## The `IsModded` Patch

R.E.P.O. has a built-in modded check — the `ModdedCheck.IsModded()` method. When this returns `true`, the game flags your session as modded which can affect matchmaking or display a modded indicator to other players.

This mod includes a Harmony prefix patch that forces `IsModded()` to always return `false`:

```csharp
[HarmonyPatch(typeof(ModdedCheck), "IsModded")]
public class ModdedCheckPatch
{
    static bool Prefix(ref bool __result)
    {
        __result = false;
        return false; // skip original method entirely
    }
}
```

**What this does:**
- `return false` on a Harmony Prefix means the original method is skipped completely
- `ref bool __result` lets us set the return value of the original method before it runs
- The game receives `false` from `IsModded()` as if no mods are present

**Why it matters:**
Without this patch, loading any BepInEx mod can trigger the modded flag. This keeps your private session clean without affecting anything gameplay-related.

---

## Disclaimer

This mod is for private use with friends and family on self-hosted sessions. Use responsibly.
