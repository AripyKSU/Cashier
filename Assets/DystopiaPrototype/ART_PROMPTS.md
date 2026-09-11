# 아트 제작 기록

## 추가 손님 3종

### Customer1.png

Create one independent game sprite, actual transparent PNG background with zero alpha outside character, no background glow. Match reference pixel clusters, cold muted blue-gray palette and front view. Original civilian: middle-aged Korean man, short cropped hair, work jacket with patched elbows. Front facing upper body head to hips with arms naturally down, exactly two arms and hands. Same framing and head scale as reference. Clear coarse pixel art, no smooth painted shading, no text, no counter, no scenery, no UI. Entire silhouette inside frame except bottom crop at thighs, 1024x1536 portrait.

### Customer2.png

Create one independent game sprite, actual transparent PNG background with zero alpha outside character, no background glow. Match reference pixel clusters, cold muted blue-gray palette and front view. Original civilian: elderly Korean woman, gray bob hair, padded coat, tired stern face. Front facing upper body head to hips with arms naturally down, exactly two arms and hands. Same framing and head scale as reference. Clear coarse pixel art, no smooth painted shading, no text, no counter, no scenery, no UI. Entire silhouette inside frame except bottom crop at thighs, 1024x1536 portrait.

### Customer3.png

Create one independent game sprite, actual transparent PNG background with zero alpha outside character, no background glow. Match reference pixel clusters, cold muted blue-gray palette and front view. Original civilian: young Korean adult man in dark knitted cap and muted gray-green rain coat. Front facing upper body head to hips with arms naturally down, exactly two arms and hands. Same framing and head scale as reference. Clear coarse pixel art, no smooth painted shading, no text, no counter, no scenery, no UI. Entire silhouette inside frame except bottom crop at thighs, 1024x1536 portrait.

선택 결과: 작업복 남성, 짧은 회색 머리 인물, 니트 모자 인물. 요청한 나이·성별 묘사는 생성 결과와 완전히 일치하지 않으므로 외형으로만 구분합니다. 4종은 완성형 Sprite이며 파츠 조합은 구현하지 않았습니다.


방식: 내장 image_gen 도구. 외부 유료 API/CLI 미사용.
모든 선택 결과는 Art/에 복사했습니다. 배경, 가판, 인물, 상품은 각각 독립 PNG입니다.
다른 게임의 실제 이미지를 참조하지 않았습니다. 생성한 Customer0.png와 Can.png를 후속 스타일 참조로 사용했습니다.

## 첫 세트 프롬프트

Seoul.png: 16:9 background only, viewed from inside a low street vendor stall looking straight out at ruined Seoul alley, worn gray concrete, damaged shutters, exposed pipes, distant barriers, bleak cold overcast atmosphere. Central lower foreground open for separately composited customer; no people, counter, foreground objects, text, signage or UI. Restrained coarse pixel clusters, limited desaturated blue-gray and ash palette, dark but legible, no smooth painting or photorealism.

Counter.png: Low battered metal and worn gray wood counter, vendor first-person viewpoint, wide horizontal canvas, broad completely empty tabletop slightly from above, straight horizontal far edge, short front apron, no tall barriers. Genuine transparent background outside counter. No products, monitor, people, hands, text, UI or exterior shadow.

Customer0.png: One adult Korean civilian woman customer, worn muted slate coat and scarf, tired restrained face, arms naturally lowered, front-facing upper body down to hips, full silhouette within frame.

Daughter.png: One small young Korean daughter, cute restrained pixel proportions, pale slightly ill-looking tired eyes, worn gray blue clothes, facing her father the viewer, exactly two arms raised gently toward viewer with exactly two empty hands, full silhouette within frame, no injuries.

Water.png: A single sealed clear drinking-water bottle with muted blue cap, unlabeled, unmistakable silhouette, front slight top-front view suitable for counter, no words, price label, extra objects.

공통 인물/생수 지시: Original dystopian ruined Seoul ration-stall game, coarse visible pixel clusters, restrained desaturated gray blue palette, cold overcast light, readable silhouette. Genuine transparent alpha PNG background, no environment/counter/UI/text/checkerboard/exterior shadow, no photorealism or smooth 3D or painting. Centered single subject with safe transparent margins.

Crackers.png: One small opened dull beige ration pouch of hardtack crackers, a few square biscuit shapes visible at mouth. No text.
Can.png: One squat sealed food tin can, ribbed dull steel, faded muted olive blank paper band, pull tab lid, no writing.
Rice.png: One rectangular sealed instant-rice tray with dull ivory peel-off lid and pale gray bowl underneath, no writing.
공통 지시: One separate inventory sprite for 2D pixel-art ruined Seoul ration stall. Recognizable small silhouette, chunky pixel clusters like a 64x64 sprite magnified, desaturated cold gray palette, front slight top-front view. One item only centered with margins. Genuine transparent alpha background, no glow or shadow outside item, no background scene, no label text or UI. Not photorealistic, no smooth painting.

## Bandage.png

Use case: stylized-concept. Independent transparent PNG inventory sprite for a 2D coarse pixel-art dystopian ration-stall game. One rolled medical gauze bandage, loose short tail, pale gray cloth. Centered item on genuine alpha transparent background with safe margin, no shadows or glow outside silhouette. Cold muted gray blue palette, readable coarse pixel clusters like a 64px item magnified, simple silhouette, front slight top-front view. No scene, counter, people, text, UI, watermark, no photorealism or smooth painted rendering. Reference image is STYLE ONLY, do not include the reference's tin can.

## Painkiller.png

Use case: stylized-concept. Independent transparent PNG inventory sprite for a 2D coarse pixel-art dystopian ration-stall game. One small silver blister strip with six pale tablets, no writing. Centered item on genuine alpha transparent background with safe margin, no shadows or glow outside silhouette. Cold muted gray blue palette, readable coarse pixel clusters like a 64px item magnified, simple silhouette, front slight top-front view. No scene, counter, people, text, UI, watermark, no photorealism or smooth painted rendering. Reference image is STYLE ONLY, do not include the reference's tin can.

## Battery.png

Use case: stylized-concept. Independent transparent PNG inventory sprite for a 2D coarse pixel-art dystopian ration-stall game. One pair of cylindrical household batteries as a single retail item, dark gray bodies with muted orange end bands, no writing. Centered item on genuine alpha transparent background with safe margin, no shadows or glow outside silhouette. Cold muted gray blue palette, readable coarse pixel clusters like a 64px item magnified, simple silhouette, front slight top-front view. No scene, counter, people, text, UI, watermark, no photorealism or smooth painted rendering. Reference image is STYLE ONLY, do not include the reference's tin can.

## Soap.png

Use case: stylized-concept. One separate transparent PNG item sprite for a dystopian ruined Seoul 2D pixel-art ration stall. One plain pale gray rectangular bar of soap with softly beveled edges, no logo or text. Readable distinct silhouette at small scale, coarse pixel clusters as 64px game art magnified, cold desaturated gray-blue palette. Centered front/slight top-front view, generous transparent margin. Real alpha transparency, not a checkerboard drawing. No environment, no other items, no exterior shadow or glow, no text or UI, no photorealism or smooth painting. Reference image: style reference only, don't reproduce its can.

## Mask.png

Use case: stylized-concept. One separate transparent PNG item sprite for a dystopian ruined Seoul 2D pixel-art ration stall. One folded gray respirator dust mask with a visible round breathing valve and two short ear straps, no printed lettering. Readable distinct silhouette at small scale, coarse pixel clusters as 64px game art magnified, cold desaturated gray-blue palette. Centered front/slight top-front view, generous transparent margin. Real alpha transparency, not a checkerboard drawing. No environment, no other items, no exterior shadow or glow, no text or UI, no photorealism or smooth painting. Reference image: style reference only, don't reproduce its can.

## Fuel.png

Use case: stylized-concept. One separate transparent PNG item sprite for a dystopian ruined Seoul 2D pixel-art ration stall. One small squat metal fuel jerrycan with integrated handle, cap and X-shaped pressed sides, subdued slate olive metal, no labels. Readable distinct silhouette at small scale, coarse pixel clusters as 64px game art magnified, cold desaturated gray-blue palette. Centered front/slight top-front view, generous transparent margin. Real alpha transparency, not a checkerboard drawing. No environment, no other items, no exterior shadow or glow, no text or UI, no photorealism or smooth painting. Reference image: style reference only, don't reproduce its can.

## Inspector.png

Use case: stylized-concept. Independent transparent PNG portrait sprite for dystopian ruined Seoul ration-stall game. One intimidating corrupt Korean ration-district inspector, front-facing upper body down to hips, worn dark official coat, restrained smug expression, graying short hair, bureaucratic realistic clothing, arms lowered. Same coarse pixel-art cluster style and desaturated cold gray-blue palette as the reference civilian, similar silhouette scale and lighting. Reference is style only; create a distinct older male identity. Entire silhouette inside frame, true alpha transparent outside the person, no glow, no background, no counter, no text, no insignia with lettering, no UI. No smooth rendering or photorealism.

## 붕대 수정

첫 결과는 실제 alpha 없이 체크무늬가 구워져 있어 사용하지 않았습니다. image_gen background-extraction으로 'Remove the entire checkerboard background, replace with genuinely transparent alpha (alpha=0 outside bandage), NOT a picture of transparency. Keep ONLY the bandage and its loose cloth tail, preserve exact coarse pixel-art silhouette, colors and orientation. No external glow or shadow. Output a real RGBA transparent PNG.'를 적용했습니다. 수정 결과의 corner alpha=0을 확인했습니다.

## 남은 확인

18장 모두 Scene에 연결했습니다. 실제 16:9 Game View에서 거래 상품·수량·한글, 인물과 가판 배치를 확인했습니다. 인물은 4개의 완성형 Sprite이며 20종 파츠화는 확장 범위로 남깁니다. 기하학적 placeholder 아트는 사용하지 않습니다. 생성 원본이 큰 픽셀 클러스터 이미지여서 640×360 정밀 수작업 픽셀 규격 정리는 남아 있습니다.
