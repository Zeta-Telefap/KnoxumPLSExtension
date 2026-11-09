# KnoxumPLSExtension

Working context, source of the **Lifter Frenzy!** mod for Baldi's Basics Plus, WIP files, notes and
design ideas from Arena.ai collaboration.

## The mod

**Lifter Frenzy!** (`knx.LifterFrenzy`, version `1.0`) — BepInEx + Harmony mod for Baldi's Basics Plus:
G picks up a school prop under the crosshair, LMB places it, RMB throws it, R + wheel rotates it,
wheel-click kicks a prop you are not holding. The throw is swept (a prop never passes a wall, a floor,
a closed door or a locker), and the game's own events act on a carried or flying prop: Flood
whirlpools, Gravity Chaos flippers, Balder Dash, Dr Reflex, the super doors, Student Shuffle,
conveyor belts and cranes, and characters walk around props instead of walking through them.

## Structure

```text
src/LifterFrenzy/                     // the mod: one partial class, one file per topic
  Plugin/Plugin.cs                    // BepInPlugin entry point (guid, name, version)
  Core/PropLifter.Core.cs             // lifecycle + shared helpers
  Core/PropLifter.Tuning.cs           // every number and name
  Core/PropLifter.State.cs            // what the runtime remembers
  Core/PropLifter.Input.cs            // G / LMB / RMB / R+wheel, the PlayerClick patch
  Core/CarryState.cs                  // one prop taken out of the world
  Carry/                              // aim, hands, body, glow
  Flight/                             // the swept throw and its types
  Placement/                          // landing a prop back into a cell
  World/                              // doors, lockers, prop names, the aim registry, sounds
  Events/                             // vanilla events and characters (one file each)
src/WIP/                              // unrelated work-in-progress files
docs/                                 // context, ideas, the design document
```

## Docs
- `docs/BUILD_MODE_DESIGN.md` — the design document (§0 is the current file layout, §16 the events)
- `docs/BUILD_MODE_IMPLEMENTATION_PLAN.md` — the implementation plan
- `docs/CONTEXT.md` — project context and current direction
- `docs/IDEAS.md` — planned features and feature backlog
- `docs/PROMPTS_AND_TASKS.md` — condensed prompt/task history
- `docs/BUGS_AND_NEXT_STEPS.md` — known issues and recommended next steps
- `docs/reference/` — source PDF and reference screenshots/text extracted from it

## Notes
- There is no `.csproj` here yet: the mod is compiled against the installed BB+ / BepInEx DLLs, and
  every file under `src/LifterFrenzy/` has to be added to that project (the mod is one `partial`
  class, so all of the files are required).
- Files in `src/WIP/` are snapshots of ongoing work and may not all compile together yet.
- The code is checked against the BB+ decompile only: it has not been compiled or run in the game here.
