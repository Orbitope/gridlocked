# Gridlocked — Sound Effects Research (CC0)

## Goal

Tactile audio feedback for both games:

| Event | Fires when | Character |
|---|---|---|
| **slide** | a piece is dragged to a new position | soft wooden shove, ~150–300 ms |
| **thock** | a piece bumps a board edge or another piece | dull wooden knock, ~80–150 ms |
| **success** | a puzzle is solved | short bright fanfare, ~1–1.5 s |
| **select** *(optional)* | a piece is picked up | subtle tick |

## Licensing constraint

The game is **CC BY-NC 4.0**. To avoid attribution burden and license-compat
questions, prefer **CC0 (public domain)** sources only. CC0 = no attribution
required, commercial+non-commercial OK, no share-alike. Safe for any project.

---

## Primary source: Kenney.nl (all CC0)

Every Kenney pack is CC0 — no attribution, downloadable as a single zip of WAVs.
License page: https://kenney.nl/t/license (and `License.txt` ships in each zip).
This is the highest-confidence, lowest-friction option. Recommended picks:

| Pack | URL | Use for |
|---|---|---|
| **Interface Sounds** | https://kenney.nl/assets/interface-sounds | select tick, UI clicks, soft confirmations |
| **Impact Sounds** | https://kenney.nl/assets/impact-sounds | **thock** — wood/clack impacts |
| **Casino Audio** | https://kenney.nl/assets/casino-audio | **slide** — card/chip slide whooshes |
| **Digital Audio** | https://kenney.nl/assets/digital-audio | **success** — jingles, confirmations |
| **Music Jingles** | https://kenney.nl/assets/music-jingles | **success** — short win fanfares |

### Suggested file picks once unzipped
(Kenney filenames are descriptive; exact names vary slightly by pack version.)

- **slide** → Casino Audio: `card-slide-*.ogg/wav`, or Interface `click_*` with a
  soft body. Fallback: a short `whoosh` from Impact.
- **thock** → Impact Sounds: `impactWood_medium_*.ogg` or `impactGeneric_light_*`.
  Wood variants read best against the puzzle theme.
- **success** → Music Jingles: `jingles_*` (the short 1–2 s ones), or Digital
  Audio `confirmation_*` / `powerUp_*`.
- **select** → Interface Sounds: `click1.ogg` / `tick_*`.

---

## Secondary source: freesound.org (filter to CC0)

For more organic / foley character. **Must set License = "Creative Commons 0"**
in the left sidebar filter, or the result may be CC-BY (needs attribution).

Pre-filtered search URLs (CC0 only):

- slide: https://freesound.org/search/?q=wood+slide&f=license:%22Creative+Commons+0%22
- thock: https://freesound.org/search/?q=wood+knock+thock&f=license:%22Creative+Commons+0%22
- success: https://freesound.org/search/?q=success+jingle&f=license:%22Creative+Commons+0%22

Search terms that work well:
- slide → "wood slide", "drawer slide", "card slide", "short whoosh"
- thock → "wood knock", "thunk", "block hit", "clack", "thock", "knock low"
- success → "success jingle", "win chime", "level complete", "fanfare short"

**Caveat:** download the WAV, then double-check the license badge on the sound's
own page — freesound mixes CC0, CC-BY, and CC-Sampling+. Only keep CC0.

---

## Backup: Sonniss GDC bundles

Annual "GameAudioGDC" bundles (https://sonniss.com/gameaudiogdc) are royalty-free
for commercial use (their EULA, not CC0, but no attribution and no per-use fee).
Huge libraries — good if you want pro foley and don't mind their license terms
instead of strict CC0. Use only if Kenney/freesound don't deliver.

---

## Recommended action

1. Download **Kenney Impact Sounds + Casino Audio + Music Jingles** (3 zips, CC0).
2. Pick one clip per event, rename to `slide.wav`, `thock.wav`, `success.wav`
   (+ optional `select.wav`).
3. Drop them in `Assets/Audio/`.
4. Assign to the `AudioManager` component (see `Assets/Scripts/Audio/AudioManager.cs`).

Keep the original `License.txt` from each Kenney zip in `Assets/Audio/` as a
record, even though CC0 requires nothing — good hygiene for the repo.
