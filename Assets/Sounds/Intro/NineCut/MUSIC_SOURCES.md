# Intro music sources

## 2026-09-29 unused asset cleanup

- Removed legacy replacement candidates `WarmDays.wav`, `Sorrow.wav`, `LonelyTheme.wav`, and `OminousDrone.wav` after the intro switched to the user-provided `PeacefulMusic.mp3` and `SadMusic.mp3`.
- Removed superseded or muted effects `BodyFall.wav`, `PotSimmer.wav`, `SlowStoneSteps.wav`, `PaperRustle.wav`, `MetalContainer.wav`, `ChopsticksDrop.wav`, `RunningStop.wav`, `DullImpact.wav`, and `ApproachingShoes.wav`.
- Their stable clip-array positions remain explicit null slots so the surviving beat indices and serialized scene data do not shift.

## 2026-09-29 user-provided father attack sounds

- `FatherAttack01.ogg` and `FatherAttack02.ogg`: created from the user-provided `Several_men_attackin_#1-1790645509310.wav` for the two father-attack beats in Scene 07.
- The first output uses approximately 0.11-0.79 seconds of the source; the second uses approximately 1.92-2.54 seconds. Each cut contains two distinct impacts and ends with a 0.06-second fade-out.
- Both outputs are stereo 48 kHz Vorbis. The original download page and license details were not supplied and still require owner confirmation.

## 2026-09-29 user-provided text and vehicle reveal sounds

- `TextBlip.ogg`: now uses the first 0.12 seconds of the user-provided `Tiny_soft_bup_sound__#4-1790650440324.wav` for Haru's dialogue.
- `FatherTextBlip.ogg`: is an 0.085-second hybrid made primarily from a slightly lowered Haru `TextBlip.ogg`, with a small amount of the separately synthesized father low tone. The 78:28 source balance places it between Haru's soft blip and the previous heavy father sound without reusing the inspector's `DialogueVoice`. Output gain is limited below clipping.
- `InspectorTextBlip.ogg`: uses the opening 0.10 seconds of the actual game dialogue asset `Assets/Sounds/SFX/DialogueVoice.wav`, with a short fade-out. Stage1 uses that same `DialogueVoice` resource in 0.10-second bursts.
- The Haru clip was sped up 2.5x to roughly 0.048 seconds, converted to mono 48 kHz Vorbis, and given a 0.01-second fade-out. Its source gain was adjusted by +8 dB.
- These replace the earlier `estudiocoati-interface-digital-de-texto-text-digital-interface-218128.mp3` and `freesound_community-medium-text-blip-14855.mp3` derivatives.
- `VehicleRevealFanfare.ogg`: replaced with the user-provided `freesound_community-tada-fanfare-a-6313.mp3`. Leading and trailing silence were removed, a short fade-out was applied, and the result was exported as stereo 24 kHz Vorbis. The existing Unity asset path and GUID were preserved.
- Original download pages and license details were not supplied with these files and still require owner confirmation.

## 2026-09-28 settlement reward sounds

- `SettlementPachinko.ogg`: `modified_coins.ogg` by mnilsson, with collaborators rubberduck and Luke.RUSTLTD. Used when the net profit row appears. Source: https://opengameart.org/content/two-tone-melody-modified-coins
- `SettlementWinJingle.ogg`: `winfretless.ogg` by Fupi. Used when the final ramen-confirmed caption appears. Source: https://opengameart.org/content/win-jingle
- Both source pages publish the files under CC0. The downloaded files are preserved without audio editing.

## 2026-09-28 user-provided replacements

- `PeacefulMusic.mp3`: `andriig-emotional-emotional-music-497354.mp3`, used as the calm music for the opening through the meal scene.
- `SadMusic.mp3`: `alex-morgan-cinematic-591327.mp3`, started after the blackout and body-fall sound. This file replaces the earlier `atlasaudio-emotional-cinematic-606246.mp3` project copy.
- `BodyFallProvided.mp3`: `freesound_community-body-fall-47877.mp3`, used for Haru's fall during the blackout.
- `BoilingWater.mp3`: `freesound_community-boiling-water-sound-62556.mp3`, looped in the ramen scene.
- These are byte-for-byte copies of the files supplied by the user. Their download pages and license terms were not supplied, so licensing still needs owner confirmation before distribution.
- The earlier Scene 07 punch layering used the existing local `BodyCollision.wav` and `DullImpact.wav`; the latest revision replaces those two attack beats with `FatherAttack01.ogg` and `FatherAttack02.ogg`.

Selected on 2026-09-23 for the user's revised direction: calm, happy music in
Scenes 01-04, then heavy, sad music from Scene 05 through Scene 09.

## WarmDays.wav

- Original title: Happy
- Composer: Alex McCulloch (Pro Sensory)
- Source: https://opengameart.org/content/happy
- Download: https://opengameart.org/sites/default/files/happy_song_1_0.mp3
- Source license: CC0 1.0, https://creativecommons.org/publicdomain/zero/1.0/
- Source tags: happy, calming, relaxing.
- Duration: 104 seconds, stereo, 44.1 kHz.
- Processing: decoded to PCM16 WAV, gain -0.34 dB to average RMS -21 dBFS,
  15 ms edge fades. No arrangement edits, time stretching or pitch changes.
- Credit: Music: "Happy" by Alex McCulloch (Pro Sensory).

## Sorrow.wav

- Original title: Emotional Piano (ensemble version)
- Composer: Centurion_of_war
- Source: https://opengameart.org/content/emotional-piano-0
- Download: https://opengameart.org/sites/default/files/emotional_piano_2.ogg
- Source license: CC0 1.0, https://creativecommons.org/publicdomain/zero/1.0/
- Source tags: piano, sad, emotional, slow. The source discussion identifies
  accompanying strings; this is the ensemble file, not the solo alternate.
- Duration: 76.8 seconds, stereo, 44.1 kHz.
- Processing: decoded to PCM16 WAV, gain +6.57 dB to average RMS -21 dBFS,
  15 ms edge fades. Peak -3.26 dBFS; no clipping, limiting or compression.
- Credit: Music: "Emotional Piano" by Centurion_of_war.

## Verification boundary

Selection is based on the creators' descriptions and numerical file analysis.
No listening-capable tool was available in this session. Neither the subjective
sound quality nor the final Unity mix has been auditioned by the assistant.
The file inspection covers duration, decodability, levels, channels, clipping
and leading/trailing silence; those checks are not listening verification.
The original procedural SFX remain; natural noodle/dish clips remain unassigned.

The earlier `LonelyTheme.wav` and `OminousDrone.wav` were removed during the unused-asset cleanup. Their provenance remains documented here. No paid service used.
