# Shop 2 texture density match — 2026-09-14

Applied to Assets/DystopiaPrototype/Art/Stage2Shop.png and Stage2Container.png.

Built-in ImageGen used on existing project artwork. Prompt: retain the original gray metal/rust palette and brightness, simplify only dense microtexture and brushed-steel streaks to the pixel-cluster density of the existing pillars and canopy. No military paint, camouflage, new large stains, material redesign or geometry changes.

Generated results were composited only into tabletop/fascia and box-panel interiors. Original alpha, dimensions, outer contours, hardware, canopy, pillars and cabinet doors were retained. Existing normal maps remain aligned to unchanged geometry. Original PNG and meta backups are in this directory; apply.ps1 records exact integration regions. Existing source rights were not independently audited; no third-party stock artwork was added.

Unity Assets/Refresh completed; edited Shop 2 verified visually in the editor Game view. Console error query returned empty. Scene and .meta hashes unchanged; GUIDs and slicing retained. git diff --check passed. No PlayMode test, no scene save/reload, no commit or push. Status: STATIC PASS.
`nRust correction: built-in ImageGen reduced orange/copper-colored patches to subdued gray-brown wear. Only existing rust-colored pixels were composited, retaining all other pixels, original alpha and dimensions. Backups and exact integration script: rust-reduction/. Scene layouts and metadata unchanged.
Rust correction: ImageGen gray-brown replacement composited only into existing orange pixels. Original alpha, geometry and neutral surface pixels retained. Backups: rust-reduction/.
