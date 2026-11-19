# UNITY_SETUP.md: turning the repository into the Unity project

The repository already contains the game logic (`Assets/_Game/Core`) and its tests (`Assets/Tests/EditMode/Core`). These steps let Unity create its own project files around them. Do this once, on your computer.

## 1. Install
1. Install **Unity Hub** (unity.com/download) and sign in with your Unity account.
2. Hub → **Installs → Install Editor** → the newest **Unity 6 LTS**. Add the module **Android Build Support** (with OpenJDK and Android SDK & NDK Tools).
3. Install **Git** and **Git LFS**, then run once:

```bash
git lfs install
```

## 2. Get the repository

```bash
git clone https://github.com/polmasanas/dark-fantasy-idle-rpg.git
```

## 3. Open it as a Unity project
1. Unity Hub → **Projects → Add → Add project from disk** → choose the `dark-fantasy-idle-rpg` folder.
2. Open it with the Unity 6 LTS version you installed. Unity notices there is no `ProjectSettings` folder and creates one, along with `Packages/manifest.json` and a `Library` cache (ignored by Git). This takes a few minutes the first time.
3. The Console may show errors about **Newtonsoft.Json** until step 4 is done. That is expected.

## 4. Add the packages the core needs
Window → **Package Manager** → **+ → Add package by name…**, one at a time:

| Package name | Why |
|---|---|
| `com.unity.nuget.newtonsoft-json` | JSON for the balance config and saves (Game.Core uses it) |
| `com.unity.test-framework` | Runs the core tests inside Unity (often already installed) |

## 5. Switch to URP 2D (decision D-010)
If you created nothing from a template, the project uses the Built-in pipeline. Install `com.unity.render-pipelines.universal` the same way as above. The rest of the URP 2D setup (renderer asset, 2D lights) is milestone M2 work, so you can skip it for now.

## 6. Check that it works
1. Window → **General → Test Runner** → **EditMode** → **Run All**.
2. All Game.Core tests should be green. These are the same tests that run in CI with `dotnet test`.

## 7. Commit what Unity created

```bash
git add -A
```

```bash
git commit -m "Add Unity project settings and packages"
```

```bash
git push
```

This adds `ProjectSettings/`, `Packages/` and the `.meta` files Unity creates next to every file. `Library/`, `Temp/` and the other cache folders are already ignored.

## If something goes wrong
- **Compile errors mentioning `Newtonsoft`**: step 4 is missing.
- **Tests not listed**: make sure the Test Runner is on the EditMode tab, and that `Assets/Tests/EditMode/Core/Game.Core.Tests.asmdef` exists.
- **Different Unity version prompt**: choose the Unity 6 LTS you installed; the repository does not pin a version yet.
