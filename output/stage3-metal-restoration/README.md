# Stage 3 brushed-metal restoration

## Correction

The previous uniform matte treatment removed the original broad metallic reflections and external contact shadows. This revision restores the supplied countertop byte-for-byte and edits the eight equipment/crate sprites to recover brushed-steel highlights, local contrast, ambient occlusion and short base contact shadows. The open pouring crate has no painted floor shadow.

## Files

- `Assets/Textures/art/Facility/CounterTop/Stage3CounterTop.png`: copied directly from `N:/개인/정총무/설비/설비3 상판.png`.
- `Assets/Textures/art/Facility/Crate/Stage3CrateClosed.png`
- `Assets/Textures/art/Facility/Crate/Stage3CrateOpen.png`
- `Assets/Textures/art/Facility/Props/Stage3FoodShelf.png`
- `Assets/Textures/art/Facility/Props/Stage3MedicineCabinet.png`
- `Assets/Textures/art/Facility/Props/Stage3NuclearProtection.png`
- `Assets/Textures/art/Facility/Props/Stage3PowerCommunications.png`
- `Assets/Textures/art/Facility/Props/Stage3PrecisionElectronics.png`
- `Assets/Textures/art/Facility/Props/Stage3ToolBench.png`

Existing .meta GUIDs, internal sprite IDs, pivots and import settings are retained; only measured sprite rectangles changed. Both crate rectangles use identical union bounds. Scene objects, layout, camera, lighting, code and Play Mode were not modified.

## Production

Built-in image_gen was used for eight sprite edits. The full initial prompts and correction prompts, source/output paths are recorded in `manifest.json`. Source artwork was supplied by the user; no external stock images were used and no additional license claim is made. Rejected generated images containing a tabletop or opaque background were not installed. `before/` preserves the pre-revision assets and metadata.

## Verification

All final sprite images were visually inspected and have genuine alpha transparency. PNG copies and retained metadata were checked against source/backup files. See `alpha-bounds.json` and `verification.json`. Unity MCP scene queries timed out; final Editor import, scene preview and Console verification remain pending. No Play Mode test was performed. Status: PARTIAL until in-scene verification is possible.
