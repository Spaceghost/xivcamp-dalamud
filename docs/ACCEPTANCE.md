# Standalone integration acceptance — 2026-10-29

Selected scope: working FFXIV plugins and the standalone Prism browser viewer.
Native Moonlight composition and hardware-specific device apps remain separate.

## Offline evidence

- Dalamud API 15 builds against reviewed 15.0.3.6 references and installed 15.0.3.9.
- Umbra toolbar adapter builds against installed Umbra 3.1.18.0.
- 14 Core tests for this module, including scene validation, old/missing providers, acknowledgement, rollback and recovery.
- Existing preview and compiled Imba output remain checked separately.
- Family layout compatibility is additive within protocol version 1; no game action
  or chat capability is added.

## Live evidence

XivCamp 0.1.0.3 and its Umbra adapter loaded in the running game. The saved
scene survived a plugin update. Campfire capture/apply/restore passed with
acknowledgements; visibility/modes of eight providers and all three observed
rectangles returned to their baseline, with the recovery journal cleared.
Externally adopted roots remain owned by their existing window host.

## Limits

Physical controller navigation, specific glasses, Windows/Android/iPadOS device
shells and native Moonlight presentation have not been accepted by these checks.
Original game controls and other providers retain their ownership.
