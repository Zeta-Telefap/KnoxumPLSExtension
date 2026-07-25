# Known Issues and Next Steps

## Recently completed
- ✅ Removed ramp/raised cell system entirely (HighWallsObjects.cs, KnoxumRampSelectionUI.cs deleted)
- ✅ Added per-layer wall texture support:
  - `SetRoomLayerWallTexture(int roomId, int layer, Texture tex)` — set texture override per layer
  - `GetRoomLayerWallTexture(int roomId, int layer)` — get texture override
  - `ClearRoomLayerWallTextures(int roomId)` — clear all overrides for a room
  - Applied via MaterialPropertyBlock in wall generation loop (both .fixed.cs and .rewrite.cs)

## Known issues in imported WIP
- WIP high-wall files may not compile together without reconciliation.
- Door/high-wall opening synchronization is unfinished.
- Some editor UI systems were in transition from inline controls to separate overlays.

## Recommended next steps
1. Decide primary branch of work:
   - high walls / wall layers / wall-slot anchors
   - Baldi customizer runtime logic
   - Dr. Reflex mini plugin polish
2. Normalize WIP source:
   - choose one authoritative `HighWallsGenerator`
   - choose one authoritative `HighWallsObjects`
   - remove obsolete duplicates
3. Add compile-checked source tree under real project layout.
4. Only after normalization, continue adding new behavior.

## Repo hygiene suggestion
- move `src/WIP/` files into final project folders only after they are reconciled
- keep `docs/reference/` as archive/reference material
- use docs to track design decisions before touching runtime-critical code again
