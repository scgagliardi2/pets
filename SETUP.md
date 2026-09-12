# SETUP.md — Local Environment Setup

One-time toolchain setup for a machine that doesn't have this project running yet. The project
itself is already scaffolded — see [README.md](README.md) for how to open and run it,
[PLAN.md](PLAN.md) for the plan, and [CLAUDE.md](CLAUDE.md) for working conventions.

Already set up on Sam's machine: Unity **6000.6.0f1** at
`/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app`. That editor binary also runs the test
suites and the scene builders headlessly (commands in CLAUDE.md, "Testing & running") — note it
needs the Editor **closed**, since an open Editor holds a lock on `/client`. There is no system
`dotnet`; the bundled SDK under the editor's
`Contents/Resources/Scripting/DotNetSdk/dotnet` is what builds Unity's generated `client/*.csproj`
if you want a typecheck without closing the Editor.

## 1. Install Unity (required, do this first)

1. **Unity Hub** — download from [unity.com/download](https://unity.com/download) and install
   it (it's the launcher that manages editor versions/projects, not the editor itself).
2. **Unity ID** — create a free account and sign into Unity Hub with it. Under license
   activation, choose the free **Personal** plan (fine for a hobby project; you'd only need
   Plus/Pro at high revenue, irrelevant here).
3. In Unity Hub → **Installs** → **Install Editor**, install the exact version pinned in
   `client/ProjectSettings/ProjectVersion.txt` (**6000.6.0f1**) rather than "whatever's current" —
   opening the project on a different version will silently upgrade and re-serialize it. Expect
   ~10GB+ for the editor alone.
4. During that install, check these modules in the module picker:
   - iOS Build Support
   - Android Build Support (bundles the Android SDK/NDK/OpenJDK — no separate Android Studio
     install needed for basic building)
   - Documentation, if you want offline docs (optional)
5. Total disk space to budget: ~15-25GB depending on modules.

## 2. iOS toolchain (Mac-specific)

1. Install **Xcode** from the Mac App Store (Unity generates an Xcode project under the hood for
   iOS builds — you build/sign/run through Xcode, not Unity directly).
2. Open Xcode once after installing to accept its license and let it install additional
   components, or run `xcode-select --install` in Terminal.
3. **Apple Developer account:** not needed yet. A free Apple ID lets you build to your own device
   for testing. The paid Apple Developer Program ($99/yr) is only needed for TestFlight/App Store
   — that's Phase 5 territory, skip it for now.

## 3. Android toolchain

1. Nothing extra required — the Android Build Support module from step 1 covers it. Only install
   Android Studio separately if you want its emulator manager or more control later.
2. To test on a real Android phone eventually: enable Developer Options + USB debugging on the
   device (not needed today).

## 4. C# editor integration

1. Since you're already in VS Code via Claude Code, that can double as your C# editor: install
   the **C# Dev Kit** extension. In Unity's Preferences → External Tools, set the external script
   editor to VS Code.
2. Alternative: JetBrains Rider has best-in-class Unity support but isn't free long-term — not
   necessary to start.

## 5. Still open

1. **A working title/codename.** Still TBD (PLAN.md §1 floats "Astromon" as a placeholder for
   anywhere a non-Pokémon-branded string is needed). The Unity project is just `client`.
2. **CI's Unity license.** `.github/workflows/ci.yml` needs `UNITY_LICENSE` (plus
   `UNITY_EMAIL`/`UNITY_PASSWORD` or `UNITY_SERIAL`) as repo secrets to activate a license in CI.
   Until those exist the client job is `continue-on-error: true`, so **CI doesn't actually gate
   merges** — remove that flag once the secrets are set.

## What's next

Open `/client` through Unity Hub and press play on `Assets/Scenes/Home.unity`. Read PLAN.md §6
Status before starting work — it's the account of what's actually built versus planned.
