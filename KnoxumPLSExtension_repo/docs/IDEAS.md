# Feature Ideas / Backlog

## High walls / room height
- independent room height per room
- independent room Y offset per room
- multi-layer wall textures by wall segment/layer
- lower-level door openings only for standard/swing doors
- windows may remain on upper level
- wall slot editing preferred over moving doors directly

## Ramps / raised geometry
- ramp placement by drag, arbitrary length
- ramp preview separate from committed data
- ramp height adjustable by dedicated overlay
- ramp mesh built from repeated 1x1 texture segments instead of stretched texture
- side walls along ramp should follow slope profile
- raised cells should create outward-facing walls on exposed edges only

## Placement and anchors
- object placement anchored to room tiles
- door placement anchored to wall slots
- eventually wall placement anchored to wall slots too
- better editor stability by updating logical anchor data first, then resolving world transforms

## Baldi customizer
### Intro types
- BB+ floor 1 intro
- BB+ floor 2 intro
- BB+ floor 3 intro
- BBCR classic-style intro (`bbcr_ClassicStyle_intro.wav`, subtitle key `Vfx_BAL_Classic_Intro`)
- BBCR party-style intro (`bbcr_PartyStyle_intro.wav`, subtitle key `Vfx_BAL_Party_Intro`)
- BBCR demo-style intro (`bbcr_DemoStyle_intro.wav`, subtitle key `Vfx_BAL_Demo_Intro`)

### Design types
- Default
- Party

### Pre-chase scenarios
- Default
- Intro -> Countdown -> Ready line
- Intro -> Ready line after walking away
- Intro -> Ready line immediately after intro
- Countdown -> Ready line

## Dr. Reflex mini plugin
- when Dr. Reflex is angry/hunting he can break breakable windows to reach the player
- first version may be simple raycast-based logic
- later version could consider path obstruction more intelligently

## Build Mode / Builder Garage (см. `BUILD_MODE_DESIGN.md`)
- перетаскивание мебели (автоматы, шкафы, столы) в режиме стройки
- перенос и снос стен (грань клетки, не объект) и дверей (синие / жёлтые)
- пристройки-комнаты модулями 1x1 … 3x3 внутри свободных клеток уровня
- Builder Garage: комната-магазин, где мебель, стены, двери и комнаты покупаются за YTP
- ресток ассортимента за сдачу собранных объектов интерьера и стен; за ресток начислять бонусные YTP
- отдельный счётчик собранного для рестока; ванильные слоты инвентаря не занимать
- dev-инспектор клетки как первый этап (проверка `constBin`, покрытия, комнат)
