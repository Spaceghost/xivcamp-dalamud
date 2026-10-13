# XivCamp architecture

Camp stores declarative arrangements of provider panels, geometry and output bindings. Providers report capabilities and actual hosting state. Applying an arrangement is a reversible transaction: preserve previous state, apply available panels, report missing providers, and restore on exit or failure. Prism owns recipient output mappings; Ghostty owns optional world hosting. Camp does not spawn game furniture, move a character or imply world placement is available merely because a scene was requested.

## Acceptance before release

- Distinguish simulated state from live state in every viewer.
- Record capabilities and failures instead of claiming unsupported behavior.
- Recover after disconnect, unload or unavailable optional providers.
- Preserve explicit input ownership and deliberate write actions.
- Measure performance on the actual target devices.
- Publish exact build, platform and hardware verification records.
