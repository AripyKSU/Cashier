# Stage 3 shop art

- Source: user-provided codex-clipboard-6c910b56-3e71-4af4-8aa7-8321b687506b.png; original pixels preserved in Assets/DystopiaPrototype/Art/Stage3Shop.png.
- Normal map: generated with OpenAI ImageGen from that reference; Assets/DystopiaPrototype/Art/Stage3ShopNormal.png. Linear RGB, uncompressed, point sampling. Uses the same UV coordinates as the source atlas.
- Five Unity sprites: Stage3Counter, Stage3Ceiling, Stage3LeftPillar, Stage3RightPillar, Stage3CeilingLamp. The original background, people, and dialogue are not used.
- Scene objects: Counter, Canopy, Stage3LeftPillar, Stage3RightPillar, Stage3CeilingLamp.
- Editing: Dystopia > Select Stage 3 Shop Parts; select a part and edit RectTransform Width/Height, position, or scale. PixelStage follows the authored RectTransform. CeilingLamp is the light's position reference.
- Dystopia > Apply Stage 3 Shop preserves existing part transforms, tint, lighting and prop placement. Only newly created parts receive initial positions. Already connected parts are left unchanged. The menu does not automatically save the scene.
- Counter baked-highlight response 0.4, specular response 0.12, rim response 0.1; source artwork is not painted over.
- Existing stage-1 prefab/art preserved. Upgrade prices, conditions, and stage-2 artwork are outside this task.
- Actual Unity Editor RenderTexture captures: hour-09.png, hour-18.png, hour-21.png. Shader errors reported as 0. PlayMode validation is pending.
- Temporary application/verification scripts have been removed. The preservation fix has only static inspection so far; Unity compilation and reapplication verification remain pending. No stage-3 backup containing the user's later layout was found in workspace backups. The scene file was not changed during this fix.
