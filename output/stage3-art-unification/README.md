# Stage 3 artwork unification

## Scope and production

- Source: user-supplied existing Stage3 artwork; edited with built-in image_gen. No external third-party stock assets were used. No additional license claim is made for source artwork.
- Final delivery: 6 facility sprites, closed crate, open crate, and countertop (9 PNG files plus their existing import metadata).
- Scene placement, code, camera, lighting, tint, GUIDs and sprite internal IDs are preserved. No scene was saved or reloaded. No Play Mode test was performed.
- Before assets and metadata: `before/` in this folder. Inspection helper is retained only here under output, outside Assets.

## Shared editing prompt

Use case: style-transfer. Production Unity 2D dystopian shop sprite edit, one standalone transparent PNG asset. Reference image 1 is the sole object to edit, reference image 2 is the current game scene only for shared art direction and lighting. Match all assets to one restrained pixel-art style: dark neutral slightly cool gunmetal steel (#42474a midtone, #24292c recesses, #818787 edge highlights), low saturation, matte lightly worn iron, subtle short scratches and tiny sparse brown oxide near joints only. No glossy broad vertical brushed-metal reflections, no photorealistic noise, no smooth 3D render. Crisp consistent dark outlines and deliberate pixel clusters. Soft overhead/front lighting with restrained upper-left highlights; front-facing verticals upright. Shared camera looks modestly down onto the work surface, about 15-20 degrees, never steep isometric. Preserve this object's function, recognisable components, orientation, front-facing layout, aspect ratio and overall silhouette/bounds closely so it replaces its sprite without moving its Unity RectTransform. Keep the original item and indicator accent colors but muted. Entire object visible, no new surrounding props, no floor plane, no cast shadow outside the object, no labels or watermark. Genuine alpha transparency around the object; remove any gray/black gradient background from input. Preserve small intentional dark holes as dark material. Do not return a screenshot or a sprite sheet; return ONLY the edited single asset.

## Asset-specific prompt requirements

| Asset | Edit requirements |
|---|---|
| Stage3FoodShelf | Tall housing left, two ration trays right. Depth recedes upper-right toward shop center. Muted olive and red parcels. Reduce overly steep top and broad reflection. |
| Stage3MedicineCabinet | Housing right, medical trays left. Mirrored inward upper-left perspective. Keep crosses, medicine bags and bottles. |
| Stage3NuclearProtection | Preserve gas mask/hood, two gauges, two canisters and pipe. Shared shallow frontal camera and matte steel. |
| Stage3PowerCommunications | Preserve power tower left, two radios and three chargers right. Correct all depth toward upper-left, showing left sides. Do not flip/reorder the assembly. |
| Stage3PrecisionElectronics | Preserve oscilloscope, warning symbol, gauge, detectors, right tower and closed pipes. Shallow frontal camera, matte steel, no smoke or detached artifacts. |
| Stage3ToolBench | Preserve vise left, four hanging tools, cup right, two drawers. Wide front view with shallow depth upper-right. |
| Stage3CrateClosed | Symmetric closed two-latch crate. Dark matte metal, remove broad white streak, small muted edge wear. |
| Stage3CrateOpen | Matching closed-crate design/material and framing; reveal empty dark interior, retain two latches. No hinged lid or contents. |
| Stage3CounterTop | Material-only restyle. Preserve original 4:3 canvas, top 58% transparency, lower countertop geometry and trapezoidal borders. Remove broad white reflection. |

## Final files and generation provenance

- `F:/UnityProject/Cashier/Assets/Textures/art/Facility/Props/Stage3FoodShelf.png`
  - Generated PNG: `C:/Users/imsoh/.codex/generated_images/01a0a651-5a43-7873-8380-4709213205d2/exec-69afad1d-ff78-4512-8fd7-4f7c498d18fc.png`
  - Preserved GUID: `b94f87ece573544468d3c2ab941498f9`
- `F:/UnityProject/Cashier/Assets/Textures/art/Facility/Props/Stage3MedicineCabinet.png`
  - Generated PNG: `C:\Users\imsoh\.codex\generated_images\01a0a651-5a43-7873-8380-4709213205d2\exec-1a306528-6465-46f5-b270-1f2fff0b103e.png`
  - Preserved GUID: `48354a4746139104f8c430144e89b323`
- `F:/UnityProject/Cashier/Assets/Textures/art/Facility/Props/Stage3NuclearProtection.png`
  - Generated PNG: `C:\Users\imsoh\.codex\generated_images\01a0a651-5a43-7873-8380-4709213205d2\exec-c35be876-0e36-43d6-9870-82c7e72a949b.png`
  - Preserved GUID: `56021a32eecae564ca4d5a558d712c43`
- `F:/UnityProject/Cashier/Assets/Textures/art/Facility/Props/Stage3PowerCommunications.png`
  - Generated PNG: `C:\Users\imsoh\.codex\generated_images\01a0a651-5a43-7873-8380-4709213205d2\exec-4e68c69a-55c0-48e3-8234-1bff53e69ed6.png`
  - Preserved GUID: `3f62e7b863807ad42bcba13f1e9b745f`
- `F:/UnityProject/Cashier/Assets/Textures/art/Facility/Props/Stage3PrecisionElectronics.png`
  - Generated PNG: `C:\Users\imsoh\.codex\generated_images\01a0a651-5a43-7873-8380-4709213205d2\exec-5cf9e3cb-7d37-4aee-877a-75346d5002ba.png`
  - Preserved GUID: `92e018ba77d83b2468945383b01f7aab`
- `F:/UnityProject/Cashier/Assets/Textures/art/Facility/Props/Stage3ToolBench.png`
  - Generated PNG: `C:\Users\imsoh\.codex\generated_images\01a0a651-5a43-7873-8380-4709213205d2\exec-fd1eec9c-982a-441e-96d2-7731fe20ae8f.png`
  - Preserved GUID: `b0eea89e97a6eaf4884fe3b1c53564b8`
- `F:/UnityProject/Cashier/Assets/Textures/art/Facility/Crate/Stage3CrateClosed.png`
  - Generated PNG: `C:\Users\imsoh\.codex\generated_images\01a0a651-5a43-7873-8380-4709213205d2\exec-ef5b3ab4-0c52-4eb9-82ed-51923b16e302.png`
  - Preserved GUID: `593344a8951822d4a8bf063ee8af6897`
- `F:/UnityProject/Cashier/Assets/Textures/art/Facility/Crate/Stage3CrateOpen.png`
  - Generated PNG: `C:\Users\imsoh\.codex\generated_images\01a0a651-5a43-7873-8380-4709213205d2\exec-8a0c57c2-a9a3-4dff-a760-086f9f240f7d.png`
  - Preserved GUID: `9cd32db9f59d7644bbcf1992df9b35e7`
- `F:/UnityProject/Cashier/Assets/Textures/art/Facility/CounterTop/Stage3CounterTop.png`
  - Generated PNG: `C:\Users\imsoh\.codex\generated_images\01a0a651-5a43-7873-8380-4709213205d2\exec-48721175-5826-4736-b882-c93dd1b21399.png`
  - Preserved GUID: `932a1ca6e1dd24c4ea3f8f6d40cb7b66`

## Verification

- All 9 generated PNG files have real transparent pixels and non-empty sprite bounds.
- Existing sprite rects were updated to measured alpha bounds only; GUIDs, sprite names/IDs and other import settings were preserved.
- Live image references resolve to updated sprites. Existing target transforms and component counts were read back.
- Scene-file SHA256 comparison against pre-edit baseline: 0 changed.
- Console errors after import: 0.
- Editor preview captured; not runtime proof. Current preview framing follows the existing editor state.
- Status: STATIC PASS. User Play Mode verification remains outstanding.

