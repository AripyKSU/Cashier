# Pending artwork update — 2026-09-10

## Completed assets
- `Assets/DystopiaPrototype/Art/Hands.png`: unchanged user source `N:/개인/정총무/손4t.png`; Unity-generated GUID b960ea489f33e484fa62df90f24bf399. Four 64×64 Sprite regions: Hand1 top-left, Hand2 top-right, Hand3 bottom-left, Hand4 bottom-right. Point filtering, no mipmaps, uncompressed. No scene placement or cursor state mapping added.
- `Assets/DystopiaPrototype/Art/DailyInstruction.png`: replaced with user source `N:/개인/정총무/지침서.png`, 224×280. Source/destination SHA256 identical; existing GUID and Single Sprite reference retained.
- `Assets/DystopiaPrototype/TopDownTest/Art/FrontContainerMale.png`: replaced with latest user source `C:/Users/PC/Downloads/상자_몸통만2배.png`, 122×102. Source/destination SHA256 identical; existing GUID 47c07e51f9acabe45b7dfb3fa9362df5 and Single Sprite fileID 21300000 retained.
- `Assets/DystopiaPrototype/TopDownTest/Art/FrontContainerNormal.png`: 122×102 linear RGB tangent normal data, aligned to the replacement chest pixels. Generated in Unity from a smoothed grayscale height field with weak gradients and neutral transparent-boundary normals. Existing GUID 8a499c0e4e268f34fac0e7606018541a retained. Existing shader samples RGB directly; no packed Unity NormalMap import format.
- `DystopiaTools.ImportPendingArtworkOnly`: explicit Assets-only menu, no scene lookup, object manipulation, SaveScene, scene reload, or Play state changes. Retained for reproducible import of these assets; it is not an automatic callback.

## Validation
- Editor assembly rebuilt at 18:59:34; asset-only menu execution succeeded.
- Hand1–4 names and rectangles, unique sprite IDs, metadata pairs, source hashes, normal dimensions, and existing normal connection checked.
- Scene SHA256 before and after: 1A96CEAB6D23EA92F29F81F29E883C5A7744C59DB7301ED4200EE98185A69D1A.
- No scene file edits, scene saves/reloads, or layout operations in this asset task.
- Runtime/Play visual check remains pending. No commit or push.

## Image-generation draft
Built-in image_gen was tried for the chest normal map. Its generated geometry did not align with the user's sprite, so it was NOT installed. `normal-draft-not-applied.png` preserves the rejected latest draft. Final installed normal is pixel-derived Unity data, not this draft. Prompt intent: convert the latest chest into a pixel-aligned OpenGL/Y-up tangent normal, neutral (128,128,255) flat areas, weak one-pixel bevels, exact original composition and silhouette. Generated drafts were not allowed to alter source art.

## Earlier unresolved work
- Scene recovery: do not resume saving or changing the scene; latest user explicitly prohibits it. Recovery copies remain under output/scene-recovery/20260910-183150.
- Trade emoji pop-up and reputation grades already have code/assets from earlier work, but in-game visual verification and missing serialized references requiring scene changes remain pending. Do not apply the scene-changing menus to finish these without a new explicit request for those scene changes.
