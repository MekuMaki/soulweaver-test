# Meddle (Soulweaver)

This folder is the full source of **Meddle (Soulweaver)**, a fork of [Meddle](https://github.com/PassiveModding/Meddle)
by PassiveModding, under Meddle's own licence, the **GNU AGPL-3.0** (`LICENSE.txt`).

## Why it exists

Soulweaver uses it to export characters, mounts and layout objects from Final Fantasy XIV to Blender. It is here
**only for testing**: the plan is Soulweaver's own exporter (phase 3), after which this fork is retired and Soulweaver
uses nothing from Meddle or MeddleTools.

## What it changes from Meddle

- A small Dalamud IPC (`MeddleFork.*`) so Soulweaver can ask for an export without Meddle's window: characters
  (humans, demihumans, monsters), a mounted character without its mount, a mount by itself, and picked layout objects.
- Its own plugin name (**Meddle (Soulweaver)**, `MeddleSoulweaver`) so it can be installed next to the official Meddle.

All the export work itself is Meddle's. Thank you, PassiveModding.

## Phases (Soulweaver)

1. Testing and confirmation: done.
2. The live link: **finishing now**.
3. Soulweaver's own exporter, baked PBR textures, MCDF characters: next (this fork is retired here).
4. Rig templates, new shaders, import without Meddle or MeddleTools.
5. Extras.
