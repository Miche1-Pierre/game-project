# AUDIO SYSTEM

_The technical side of audio. Direction and assets: `../06_AUDIO/`._

## Approach
- A central audio service plays one-shots and loops by key, so gameplay code does not hold AudioSource references.
- Spatial (3D) audio for world sounds; 2D for UI.
- Proximity voice, if the concept uses it, via Steam voice or a proven package (decided in `../07_MULTIPLAYER/VOICE.md`).
- Keep the mixer simple: SFX, Ambience, Voice, UI buses.

## Rule
Every important gameplay event should be able to fire a sound through the service (`../06_AUDIO/SOUND_DESIGN.md`).
