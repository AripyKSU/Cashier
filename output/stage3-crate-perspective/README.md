# Stage3 crate perspective correction

- Edited only Stage3CrateClosed.png, Stage3CrateOpen.png and their sprite rects.
- Same matte metal style; increased top-surface visibility and foreshortened front wall/latches to match the counter's downward view.
- Closed/open use the same crop (64,100,1323,865) for consistent framing. GUIDs and internal sprite IDs preserved.
- Original files and metadata retained in before/. No scene save or reload, transform edits, or Play Mode changes.
- Built-in image_gen used. Sources are the project's user-provided crate and countertop images.

## Final files

- Assets/Textures/art/Facility/Crate/Stage3CrateClosed.png
- Assets/Textures/art/Facility/Crate/Stage3CrateOpen.png

## Closed prompt

Use case: precise-object-edit. EDIT image1 closed steel crate. Image2 is the counter whose steep downward viewing perspective the crate MUST match. The prior crate is much too front-on: lid only 20% of silhouette height and front wall 80%. Change camera pitch substantially, to about 40-45 degrees looking DOWN, and redraw the SAME physical crate at that angle. The TOP LID trapezoid must occupy about 50% of total silhouette height, and FRONT WALL the remaining 50%. Top plane depth edges must recede toward upper center, parallel in perspective to counter rails. Both front latches foreshortened vertically with the front wall. Straight horizontal front edges, symmetric left/right, NO sideways yaw and NO image-plane rotation. Do NOT just distort/squash the whole image: reveal the previously unseen top surface and foreshorten front face coherently. Preserve exact dark matte gunmetal palette, sparse brown oxide, two latches, corner reinforcements, texture style, all hardware and closed state. No glossy vertical reflection, no new handles or logos. Keep overall occupied sprite width:height close to current 1.58:1, use depth to replace front height, not make the entire sprite taller. Genuine transparent PNG background. One standalone entire closed crate, no table or scene. This is specifically a camera-angle correction; material is already approved and must not change.

## Open prompt

Use case: precise-object-edit. Image 1 is the authoritative NEW downward-view CLOSED crate. Image 2 is the OLD open state, only to explain that this crate can be lidless, not as a camera or proportions reference. Make ONLY the matching OPEN lidless variant of image 1. Preserve image1 camera pitch (about 40-45 degrees looking down), bounding box, all outer corners, front wall height, both latches, rivets, corner reinforcements, colors, rust marks, pixel texture and exact positions. Remove the flat closed lid TOP surface only, revealing an empty dark steel interior: internal walls and bottom visible from this elevated camera, opening occupies the same large trapezoid previously occupied by lid. No upright lid, no hinge protrusion, no contents. Front wall must NOT grow taller; front face and latches remain exactly as image1. Must be a frame-swap pair aligning perfectly. Genuine transparent PNG, same canvas dimensions/aspect/framing as image1, full single crate only, no scene or floor.

