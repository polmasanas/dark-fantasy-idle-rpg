# UNITY_SETUP.md: turning the repository into the Unity project

The repository already contains the game logic (`Assets/_Game/Core`) and its tests (`Assets/Tests/EditMode/Core`), but no Unity settings yet. Unity Hub may refuse to open a folder without settings, so we let Unity create a fresh **Universal 2D** project (decision D-010) and copy its settings into the repository. Do this once, on your computer.

## Phase 1: Install the tools
1. Install **Git** (git-scm.com) and **Git LFS** (git-lfs.com). Then, in a terminal (Windows: "Git Bash"), run once:

```bash
git lfs install
```

2. Install **Unity Hub** (unity.com/download) and sign in with a Unity account (create one if needed). When asked for a licence, choose **Unity Personal** (free).
3. In Unity Hub: **Installs → Install Editor** → pick the newest **Unity 6 LTS** version (6000.x, marked LTS).
4. In the module list, tick **Android Build Support**, including its sub-items **OpenJDK** and **Android SDK & NDK Tools**. Add **Visual Studio** (Windows) if you don't have a code editor. The download is several GB.

## Phase 2: Get the repository

```bash
git clone https://github.com/polmasanas/dark-fantasy-idle-rpg.git
```

```bash
cd dark-fantasy-idle-rpg
```

```bash
git checkout m1/core-skeleton
```

The game code currently lives on the `m1/core-skeleton` branch; `main` gets it when M1 is merged.

## Phase 3: Create a template project
1. Unity Hub → **Projects → New project** → Unity 6 LTS → template **Universal 2D**.
2. Name it `DFIRPG-Template`, save it **outside** the repository folder (e.g. your Desktop), and create it.
3. When the editor has finished opening, **close Unity**.

## Phase 4: Copy the settings into the repository
With your file explorer, copy from `DFIRPG-Template` into `dark-fantasy-idle-rpg`:

| Copy this | Into the repository as |
|---|---|
| `DFIRPG-Template/ProjectSettings/` | `dark-fantasy-idle-rpg/ProjectSettings/` |
| `DFIRPG-Template/Packages/` | `dark-fantasy-idle-rpg/Packages/` |
| `DFIRPG-Template/Assets/Settings/` (URP renderer assets) | `dark-fantasy-idle-rpg/Assets/Settings/` |
| `DFIRPG-Template/Assets/Scenes/` | `dark-fantasy-idle-rpg/Assets/Scenes/` |

Copy each folder **with its matching `.meta` file** if one sits next to it (e.g. `Settings.meta`). Do not copy `Library/`, `Temp/`, `Logs/` or `UserSettings/`. After that the template project can be deleted.

## Phase 5: Open the repository in Unity
1. Unity Hub → **Projects → Add → Add project from disk** → choose the `dark-fantasy-idle-rpg` folder → open it with Unity 6 LTS.
2. The first import takes a few minutes. The Console may show errors about **Newtonsoft.Json**; Phase 6 fixes them.

## Phase 6: Add the packages the core needs
Window → **Package Manager** → **+ → Add package by name…**, one at a time:

| Package name | Why |
|---|---|
| `com.unity.nuget.newtonsoft-json` | JSON for the balance config and saves (Game.Core uses it) |
| `com.unity.test-framework` | Runs the core tests inside Unity (usually already installed) |

## Phase 7: Check that it works
1. Window → **General → Test Runner** → **EditMode** tab → **Run All**.
2. All 36 Game.Core tests should be green. They are the same tests CI runs with `dotnet test`.

## Phase 8: Commit what Unity created

```bash
git add -A
```

```bash
git commit -m "Add Unity project settings, URP 2D template assets and packages"
```

```bash
git push
```

This adds `ProjectSettings/`, `Packages/`, the URP settings and the `.meta` files Unity creates next to every file. `Library/`, `Temp/` and other caches are already ignored.

## If something goes wrong
- **"An Application Control policy has blocked this file (0x800711C7)"** (e.g. `BeeBuildProgramCommon.dll`): Windows 11 **Smart App Control** is blocking Unity's script compiler. Windows Security → App & browser control → Smart App Control → **Off**, then restart Windows. On many Windows versions it cannot be turned back on without reinstalling Windows. On a work-managed PC, IT has to allow Unity instead.
- **Unity Hub `.msix` fails in a third-party "MSIX Installer"**: install it with Windows' own App Installer (right-click → Open with → App Installer) or PowerShell `Add-AppxPackage -Path "$env:USERPROFILE\Downloads\UnityHubSetup-x64.msix"`.
- **Compile errors mentioning `Newtonsoft`**: Phase 6 is missing.
- **Tests not listed**: check the Test Runner is on the EditMode tab and `Assets/Tests/EditMode/Core/Game.Core.Tests.asmdef` exists.
- **"Project was created with a different version"**: choose the Unity 6 LTS you installed.
- **Hub says the folder is not a valid project**: Phase 4 is incomplete (`ProjectSettings/` must be inside the repository folder).
