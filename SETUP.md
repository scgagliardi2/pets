# SETUP.md — Local Environment Setup

Steps to get your machine ready before we start scaffolding the project. See [PLAN.md](PLAN.md)
for the project plan and [CLAUDE.md](CLAUDE.md) for working conventions.

## 1. Install Unity (required, do this first)

1. **Unity Hub** — download from [unity.com/download](https://unity.com/download) and install
   it (it's the launcher that manages editor versions/projects, not the editor itself).
2. **Unity ID** — create a free account and sign into Unity Hub with it. Under license
   activation, choose the free **Personal** plan (fine for a hobby project; you'd only need
   Plus/Pro at high revenue, irrelevant here).
3. In Unity Hub → **Installs** → **Install Editor**, pick the current **LTS** release (don't
   pick a "Tech Stream"/beta version — LTS is the stable one). Expect ~10GB+ for the editor alone.
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

## 5. Decisions to have ready (quick, not installs)

1. A working title/codename for the project (used as the Unity project name).
2. A placeholder bundle identifier in reverse-DNS form, e.g. `com.<yourname>.pets` — cosmetic
   now, but Unity asks for it during project creation and it's annoying to rename later.

## What's next

Once Unity is installed and you've created a blank project (Unity Hub → New Project → **2D
Core** template, pointed at `/client` in this repo), let's scaffold from there: a Unity-specific
`.gitignore`, the folder structure from PLAN.md (`Scripts/Simulation`, `Scripts/Data`,
`Scripts/Gameplay`, etc.), and starter C# files.
