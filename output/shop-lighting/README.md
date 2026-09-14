# Shop 2 / 3 lighting repair — 2026-09-14

User requested matched lighting for Shop 2 counter/box and Shop 3 box, and authorized scene lighting changes while retaining layout.

## Applied

- Stage2Reference: re-enabled counter normal response at 0.4 and frame response at 0.25; restrained rim/specular response. Box normal response 0.65, highlight response 0.78, specular 0.08, bottom shade 0.55.
- Stage3Reference and the currently edited Stage 3: added matching container normal, response 0.45, highlight response 0.8, specular 0.08, bottom shade 0.45.
- SetStage3Container now retains the matching normal and response when the stage is selected again.
- Original color textures, sprite slicing, pivots, sizes, positions, parent relationships, Shop 1 and shared shader were preserved.

## Generated asset / provenance

Assets/DystopiaPrototype/Art/Stage3ContainerNormal.png, generated using built-in ImageGen from the existing project-owned Stage3Container.png. No new external stock asset. Rights to the existing source were not independently audited.

Prompt: Convert the exact reference to an opaque RGB OpenGL tangent-space normal map; R right, G up, B toward viewer; 1536x1024; preserve silhouette, coordinates, proportions, margins and every edge; flat front plates, upward-facing thin top rims, inward-facing inner walls; subtle structural bevels; do not turn rust/paint into relief; no albedo, lighting, glow, text, cropping or redesign.

Both source and generated map are 1536x1024. Unity generated the new .meta GUID aadbeaa232d6eca4080131483bbe4824. Imported as uncompressed linear RGB with Point filtering, clamp wrapping, no mipmaps, default-platform max size 512, matching the box's imported size policy. It is a generated surface estimate, not a mesh bake.

## Preservation / verification

- `20260914-095006/editing-before.unity` preserves the initial unsaved editing state; `disk-before.unity` preserves the disk version.
- `reference-backup` preserves both original reference scenes; `changes.json` lists the exact field changes.
- `20260914-095201/editing-before.unity` and `editing-after.unity`: all 28 serialized Transform/RectTransform blocks identical. The live scene was never reloaded; its disk file was not overwritten.
- Reference scenes: all serialized Transform/RectTransform blocks identical before/after.
- Existing GUIDs retained; new normal GUID occurs in exactly one asset .meta. Source/normal dimensions match.
- Unity compilation succeeded with 0 reported warnings before applying the tuning. Before/after editor renders verified the active Stage 3 change (`20260914-095201/before.png`, `after.png`).
- One preview-scene save attempt was rejected by Unity (preview scenes cannot be saved); no reference was saved by that attempt. The references were then updated with bounded field-only edits instead.
- Windows subsequently locked; final reimport/compile confirmation and Stage 2 rendered verification could not complete. Play Mode was not entered.
- Temporary menu code was removed from Assets; its text is retained only in `verification-menu.cs.txt` as QA evidence. No runtime helper was introduced.
- The current active scene retains the user's pre-existing unsaved changes plus the applied Stage 3 lighting changes. Reference files and new normal asset are saved on disk. No commit/push/merge.

Overall status: PARTIAL until final Unity compile/reimport and Stage 2 rendering can be checked after Windows unlock. Actual PlayMode verification remains pending.
