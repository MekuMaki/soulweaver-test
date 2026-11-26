# Soulweaver (test build 0.2.109.0)

Soulweaver links Final Fantasy XIV and Blender: send characters, mounts, the camera and world objects to Blender in
one click, and pose and animate them live, in GPose and outside it. **Soulweaver Studio** is its Blender add-on.

> This is a **test build** for invited testers. Please don't share the link around yet.

## Install (about two minutes)

1. In game, open Dalamud settings > **Experimental** > **Custom Plugin Repositories** and add:
   `https://raw.githubusercontent.com/MekuMaki/soulweaver-test/dev/repo.json`
   Tick the box, then **Save**.
2. In the plugin installer, install **Soulweaver**.
3. Type `/soulweaver` in game. The first time you send something, Soulweaver finds Blender (5.2 or newer) and offers to
   install **Soulweaver Studio** in it. Say yes, and that's all.

Updates come through Dalamud like any other plugin; Soulweaver Studio updates itself in Blender when you send.

## Phases

| Phase | What | Status |
| --- | --- | --- |
| 1 | Testing and confirmation: Send to Blender in one click | Done |
| 2 | The live link: GPose ⇄ Blender posing (Brio and Ktisis), Game / Layer / Blender control, Record, every character type, mounts, the camera link with depth of field, Multiselect and parallel sends | Done |
| 3 | Soulweaver's own exporter, baked PBR textures, the new shader setup, the game's environment, sun and lights in Blender | **In progress** |
| 4 | Rig templates | Later (the big one) |
| 5 | Extras: MCDF characters exported from GPose and loaded outside it, mod files, VFX, VRM export, NPR shaders, rendering an animation to image sequences, in-game render settings, the GPose gizmo | Later |

## Licence

Soulweaver and Soulweaver Studio: MIT (see `LICENSE`). Build on it freely.

Soulweaver is a fan project, not affiliated with Square Enix. Everything it does is on your PC only: nothing is sent
to the game's servers.
