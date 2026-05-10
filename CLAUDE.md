# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project: Magnet Panic: Scrapstorm

A 10-day game-jam arena combat micro-roguelite. Core fantasy: **you don't fight enemies — you turn them into ammunition.** Three verbs drive everything: **Pull / Strike / Repel**. Target platform is **WebGL**, delivered via a React host page that embeds the Unity build.

The single source of truth for mechanics, tuning numbers, enemies, upgrades, pacing, and MVP scope is `design/magnet_panic_scrapstorm_gdd_gamejam.md`. Read it before designing or balancing anything — most "what should X do?" questions are already answered there with concrete values.

## Repository layout

This is a **two-part repo** with no shared build system:

- `MetalPanic/` — Unity 6 project (`6000.3.6f1`), URP, new Input System, 2D + 3D physics modules enabled. Game logic lives here. Main scene is `Assets/Scenes/SampleScene.unity`. Open the folder directly in Unity Hub with the matching editor version.
- `react-app/` — Vite 8 + React 19 + TypeScript host page that will embed the Unity WebGL build via `react-unity-webgl`, with `zustand` for state. Currently still the Vite starter template (`src/App.tsx`) — the Unity embed has not been wired up yet.
- `design/` — GDD and design docs (Spanish).
- `.claude/` — agents, skills, rules, hooks for the Claude Code Game Studios workflow.
- `.agents/skills/` — skill definitions used by the orchestration agents.

There is no top-level package manifest; each part is built independently.

## Commands

All `npm` commands run from `react-app/`:

```
npm run dev       # vite dev server
npm run build     # tsc -b && vite build (typecheck must pass)
npm run lint      # eslint .
npm run preview   # serve the production build
```

Unity build: open `MetalPanic/` in Unity 6000.3.6f1 and use **File → Build Profiles → WebGL → Build**. The output goes into the React app's `public/` (or wherever `react-unity-webgl` is configured to load it from) — that wiring is TBD.

There is no test runner configured in either project yet.

## Architecture notes that aren't obvious from the file tree

- **Game runs inside the React shell, not standalone.** The shipped artifact is the Vite-built page hosting the Unity WebGL canvas. Anything that needs to talk to the page (score submission, share buttons, page-level UI) goes through `react-unity-webgl`'s message bridge — not direct DOM access from Unity.
- **Zustand is the React-side state store.** It exists for host-page state (run results, menu state, settings) — it is not a substitute for Unity's own state. Don't try to mirror gameplay state across the bridge; keep gameplay authoritative inside Unity and only push summary events out.
- **WebGL is a hard constraint, not a "nice to have".** Avoid features the WebGL player drops or chokes on (threading, certain audio APIs, large uncompressed textures, expensive reflection). The 10-day roadmap ends with WebGL optimization on Day 9 — design with that target from day one.
- **GDD numbers are tuning starts, not contracts.** Variables in the GDD (radius 5m, capacity 8, counter window 0.35–0.5s, etc.) are explicit suggested defaults. Treat them as the place to begin and to converge back to during balance passes.

## .claude/rules — read with care

The files in `.claude/rules/` (`engine-code.md`, `gameplay-code.md`, `ui-code.md`, etc.) are scaffolded from a generic Claude Code Game Studios template and **do not yet match this repo**:

- Their `paths:` frontmatter targets `src/core/**`, `src/gameplay/**`, `src/ui/**`, `prototypes/**`, `docs/engine-reference/` — none of which exist here.
- The example code is **GDScript (Godot)**, but this project's engine is **Unity (C#)**.

The *principles* (zero-alloc hot paths, data-driven gameplay values, UI never owns game state, delta-time everywhere, etc.) still apply and are good guidance. The *paths and code samples* should not be treated as authoritative for this repo until someone retargets them. If you write or edit a rule file, update its `paths:` to point at real Unity directories (e.g. `MetalPanic/Assets/Scripts/...`) and convert examples to C#.

## Conventions worth preserving

- Game scope is locked to the **MVP technical list** in GDD §17. New mechanics that aren't on that list should be questioned before being implemented — the jam constraint is real.
- Design doc is in Spanish; code, identifiers, and code comments stay in English.
