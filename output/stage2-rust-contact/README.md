# Stage 2 rust and contact-shadow revision

## Assets (built-in image_gen)
- `Assets/Textures/art/Facility/Frame/Stage2RustedFrame.png`
- `Assets/Textures/art/Facility/Frame/Stage2RustedClock.png`
- `Assets/Textures/art/Facility/Crate/Stage2RustedCrateClosed.png`
- `Assets/Textures/art/Facility/Crate/Stage2RustedCrateOpen.png`

Source: existing user-provided project art. No new external source used. Original textures preserved.
Material reference: `Assets/Textures/art/Facility/CounterTop/Stage2CounterTop.png`.

## Generation prompt summary
Edit the original frame, clock, closed crate, and open crate separately. Preserve structural silhouettes, hardware, perspective, dark outlines, transparent background, and sprite sheet placement. Match the countertop reference: gray brushed steel with visible irregular reddish-brown corrosion clusters and polished edge glints. Do not add text, objects, or exterior cast shadows. Clock display stays empty and green. Open crate stays open, closed crate stays closed.

## Connections
Current Stage 2 scene only: Canopy, Stage3LeftPillar, Stage3RightPillar, CounterClock, FrontContainer, PouringContainer. The legacy pillar object names are reused by stage-switch menus; the name does not identify the current sprite's stage. Stage 1/3 reference scenes were not edited. Frame and clock keep original sprite-sheet slicing. Crates are tightly cropped through Unity's Sprite importer. No PNG edits were performed outside image_gen.

## Shadow correction
DystopiaPixelStage supplies actual footprint height and sprite UV to PixelStageLighting. Facility/FrontContainer shadows find each column's lower opaque contour rather than projecting transparent padding. The contact overlaps the base by two design pixels, fading over a short distance. This shared renderer correction applies to those named objects in other stages as well. Clock shadow remains disabled.

## Verification
- Unity compilation returned no errors; Console error query empty.
- Editor preview inspected: `output/facility-preview/front-20260916-072704.png`.
- 303 serialized blocks before and after asset assignment; no added/removed scene objects.
- Prefab changes: sprite references and tint only; authored RectTransforms preserved.
- All four new PNGs have Unity-generated metadata.
- Before/after scene copies: `output/stage2-rust-contact/20260916-072651/`.
- Current scene left dirty, not saved or reloaded. Play Mode not run.
