# Soulweaver (test build 0.2.80.0)

Soulweaver links Final Fantasy XIV and Blender: send characters, mounts, the camera and world objects to Blender in
one click, and pose and animate them live, in GPose and outside it. **Soulweaver Studio** is its Blender add-on.

> This is a **test build** for invited testers. Please don't share the link around yet.

## Install (about two minutes)

1. In game, open Dalamud settings > **Experimental** > **Custom Plugin Repositories** and add:
   `https://raw.githubusercontent.com/MekuMaki/soulweaver-test/dev/repo.json`
   Tick the box, then **Save**.
2. In the plugin installer, install **Soulweaver** and **Meddle (Soulweaver)**. Both are needed for now.
3. Type `/soulweaver` in game. The first time you send something, Soulweaver finds Blender (5.2 or newer) and offers to
   install **Soulweaver Studio** in it, and **MeddleTools** too if it's missing. Say yes, and that's all.

Updates come through Dalamud like any other plugin; Soulweaver Studio updates itself in Blender when you send.

## Why a Meddle fork, for now

Exporting from the game currently uses **Meddle (Soulweaver)**, a fork of
[Meddle](https://github.com/PassiveModding/Meddle) by PassiveModding, and Blender uses
[MeddleTools](https://github.com/PassiveModding/MeddleTools) for the game's shaders. Both are excellent and both are
used here **only for testing**. The plan (phases 3 and 4 below) is Soulweaver's own exporter, its own baked textures
and its own import in Blender, after which Soulweaver won't use anyone else's tools at all. The fork can sit next to
the official Meddle without clashing. Its full source is in [`meddle-fork/`](meddle-fork) as Meddle's AGPL-3.0
licence asks.

## Phases

| Phase | What | Status |
| --- | --- | --- |
| 1 | Testing and confirmation: Send to Blender in one click | Done |
| 2 | The live link: GPose ⇄ Blender posing (Brio and Ktisis), Game / Layer / Blender control, Record, every character type, mounts, the camera link with depth of field, Multiselect and parallel sends | **Finishing now** |
| 3 | Soulweaver's own exporter (Meddle fork retired), baked PBR textures, MCDF characters exported from GPose and loaded outside it | Next |
| 4 | Rig templates, the new shader setup, and import in Blender without Meddle or MeddleTools | Later (the big one) |
| 5 | Extras: mod files, VFX, VRM export, NPR shaders, rendering an animation to image sequences, in-game render settings, the GPose gizmo | Later |

## Licences

- **Soulweaver and Soulweaver Studio:** MIT (see `LICENSE`). Build on it freely.
- **Meddle (Soulweaver):** a fork of Meddle, under Meddle's own GNU AGPL-3.0. Source in `meddle-fork/`.
- **MeddleTools:** not included; Soulweaver Studio installs it from its own repository, under its own licence.

Soulweaver is a fan project, not affiliated with Square Enix. Everything it does is on your PC only: nothing is sent
to the game's servers.
