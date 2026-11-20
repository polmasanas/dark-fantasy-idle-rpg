# ANDROID_DEVICE_TEST.md: first build on a phone (or the emulator)

> M4-4: these checks need a real Android phone over USB. Until you have one, the **Android emulator** on the PC can be used for the **save / app-kill checks only** (the emulator says nothing about phone performance). Results go into the Jira task "M4 device checks" under the M4 epic.

## 1. One-time setup (PC)
1. **Unity Hub → Installs →** the ⚙ on **6000.3.29f1** → **Add modules** → tick **Android Build Support** with **OpenJDK** and **Android SDK & NDK Tools**. Install (about 5 GB).
2. Open the project → **File → Build Profiles** → select **Android** → **Switch Platform** (the first switch re-imports everything and takes a few minutes).
3. In the same window, tick **Development Build** (the dev panel only exists in development builds).
4. **Edit → Project Settings → Player → Android tab → Other Settings**, check:
   - Package Name: `com.dfirpg.placeholder` (placeholder until M12; already set)
   - Scripting Backend: **IL2CPP**; Target Architectures: **ARM64** (add **x86-64** only if you use the emulator, see §3)
   - Default orientation: **Portrait** (already set)

## 2. Path A: your phone over USB
1. On the phone: **Settings → About phone** → tap **Build number** 7 times (developer mode). Then **Settings → System → Developer options** → turn on **USB debugging**.
2. Connect the USB cable; on the phone accept **"Allow USB debugging?"** (tick "Always allow from this computer").
3. In Unity: **File → Build Profiles → Android → Run Device** should list the phone (press **Refresh** if not). Click **Build and Run**, save the `.apk` in a `Builds/` folder (it is ignored by Git). The game starts on the phone.
4. Logs: in Git Bash, `"$ANDROID_SDK/platform-tools/adb" logcat -s Unity` (Unity shows the SDK path under **Preferences → External Tools**), or install the **Android Logcat** package from the Package Manager.

## 3. Path B: the Android emulator (no phone)
1. Install **Android Studio** (free) → **More Actions → Virtual Device Manager → Create device** → Pixel 6 → system image **API 34, x86_64** ("Google APIs") → Finish → ▶ start it.
2. In Unity Player settings tick **x86-64** in Target Architectures as well as ARM64 (builds get bigger; untick it again when you stop using the emulator).
3. With the emulator running, it appears in **Run Device** like a phone: **Build and Run**.
4. Use the emulator for checks **2, 3, 4 and 5** below. **Do not use it for check 1** (performance).

## 4. The checks
Write each result (pass/fail, numbers, phone model and Android version) in the Jira task.

| # | Check | How | Pass when |
|---|---|---|---|
| 1 | **Offline speed** (phone only, M4-3) | Play 2–3 minutes, then dev panel → **Simulate 12 h away**. Read the time in the status line ("simulated 6 h 00 min of fighting in N ms"). Repeat 3 times | **N < 200 ms** every time. If not, tell Claude: the fallback is a summary fight mode |
| 2 | **Save on app kill** | Play until something changes (a level, a boss), wait 30 s (autosave) or press **Save now**, then swipe the app away from Recents. Reopen | Level, gold, bag and settings are as before (at most the last 30 s lost) |
| 3 | **Kill right after a key moment** | Right after a boss dies, swipe the app away at once. Do it 5 times | The game always opens; never "save could not be read" |
| 4 | **Back from the background** | Press Home, wait 3+ minutes, return | The "While you were away" report appears with about half the time credited; **Collect** moves items into the bag |
| 5 | **Clock tricks** | Close the game. **Settings → System → Date & time** → turn off automatic time → set the clock **5 hours ahead** → open the game. Then set it back to the real time (automatic on) and open again | Ahead: the report says the clock jumped and only real time counts. Back: no time credited. Progress never lost |
| 6 | **Reboot** | Close the game, restart the phone, wait 10 minutes, open | The report credits about 10 minutes away |

Save files are in `/sdcard/Android/data/com.dfirpg.placeholder/files/` (`save.dfrs` and `save.bak1..3`); `adb shell ls -l` on that folder shows them.

## 5. If something fails
Copy the first red lines from Logcat (or the Unity Console) and a short description to Claude. Do not delete the save files: they help reproduce the problem.
