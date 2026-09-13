# Stage 1 prop normal maps
Generated with the built-in ImageGen tool, 2026-09-13.
References: current Stage1WoodContainerCompact.png and Stage1BasicClock.png, provided by the user.
Prompt shared constraints: Convert the exact reference into an opaque OpenGL tangent-space RGB normal map (R right, G up, B toward viewer). Preserve normalized coordinates, silhouette, aspect ratio, padding and part placement. Neutral background RGB 128,128,255. Broad planar normals and subtle pixel-art relief; no painted lighting, text, new parts, recropping or redesign.
Box prompt: wooden front planks mostly +Z, upper rim +Y, thin sides outward +/-X, subtle recessed joints and restrained corner bevels.
Clock prompt: front frame +Z, housing top +Y, sides +/-X, chamfered frame, recessed empty display, buttons and feet preserved.
Outputs: Assets/DystopiaPrototype/TopDownTest/Art/Stage1WoodContainerCompactNormal.png; Assets/DystopiaPrototype/Art/Stage1BasicClockNormal.png.
The maps are AI-derived surface estimates, not normals baked from a 3D mesh. Rendered outline recovery is not yet verified.
