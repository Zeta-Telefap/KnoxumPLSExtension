# Project Context

## Core project
`KnoxumPLSExtension` is a mod/plugin project centered around extending Plus Level Studio / BB+ editor behavior and runtime behavior.

## Major active workstreams
1. **High walls / multi-layer rooms**
   - per-room height
   - per-room vertical offset
   - generated extra walls/floors in editor and runtime
   - runtime roomId mapping before playtest
2. **Raised cells and ramps**
   - per-cell raised platforms
   - ramp placement and later polish
   - editor/runtime rebuild logic
3. **Door / wall / object anchor systems**
   - move doors and later wall slots in logical tile space
   - anchor objects to room tiles instead of loose world positions
4. **Baldi customizer**
   - per-level mode settings for Baldi intro, skin, and pre-chase scenario
5. **Mini plugin ideas**
   - Dr. Reflex can break windows while angry/hunting

## Key shared assumptions
- Editor and runtime often need separate handling.
- Compile/play requires explicit runtime maps because runtime ordering can differ from editor ordering.
- Direct world-space hacks are fragile; tile/room anchored data is preferred.
- Many systems are still WIP and should be treated as snapshots, not final architecture.

## Current repo import state
This repo started empty. The contents added here are imported working notes and source snapshots from the Arena.ai workspace so future work can continue from a documented state.
