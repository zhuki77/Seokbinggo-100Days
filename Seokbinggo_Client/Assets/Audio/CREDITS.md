# Audio asset provenance

This manifest covers the audio files under `Assets/Resources/Audio`.

## Current status

The repository contains five BGM/layer WAV files and nineteen runtime SFX cue OGG files. Three SFX (InvasionAnnounced, HypothermiaEntered, FrostMineralRevealed) are sourced from Kenney (CC0). The remaining BGM and SFX files have no confirmed provenance. Treat every unconfirmed file as internal-demo-only until the producer fills in the provenance fields below. Do not claim CC0 or redistribute those files externally without evidence.

## Delivery inventory

| Category | Runtime path | Intended use | Author/source/license |
|---|---|---|---|
| BGM | `Audio/BGM/Day` | Day loop | Pending |
| BGM | `Audio/BGM/Night` | Night loop | Pending |
| BGM | `Audio/BGM/Boss` | Boss loop | Pending |
| BGM | `Audio/BGM/Title` | Title loop | Pending |
| BGM layer | `Audio/BGM/BaekjungPercussion` | Baekjung percussion layer | Pending |
| SFX | `Audio/SFX/InvasionAnnounced` | Invasion warning bell | [Kenney Impact Sounds](https://kenney.nl/assets/impact-sounds) · `impactBell_heavy_000.ogg` · CC0 |
| SFX | `Audio/SFX/HypothermiaEntered` | Hypothermia state enter | [Kenney Impact Sounds](https://kenney.nl/assets/impact-sounds) · `impactGlass_light_000.ogg` · CC0 |
| SFX | `Audio/SFX/FrostMineralRevealed` | Frost mineral discovery | [Kenney Impact Sounds](https://kenney.nl/assets/impact-sounds) · `impactGlass_heavy_000.ogg` · CC0 |
| SFX | `Audio/SFX/<other 16 AudioCues>` | Remaining cue files | Pending |

`NapStarted.ogg` is retained as an unused legacy delivery and is not routed by the current product audio cue enum.

## Release checklist

- Record the original author or creator for every file.
- Record the direct source URL or internal delivery reference.
- Attach the exact license name and a copy or permanent link to its terms.
- Record whether attribution, modification disclosure, or share-alike text is required.
- Replace any file whose redistribution rights cannot be demonstrated.
