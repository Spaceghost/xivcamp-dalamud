# XivCamp

Unfold your little corner of Eorzea.

A working saved-workspace plugin for the illustrated UI family, with Campfire,
Work corner and Adventure arrangements. Remember your current workspace, unfold
a saved arrangement, and pack up to restore what was there before.

## Use

Open `/xivcamp` or its optional Umbra toolbar button. Choose an arrangement and
**Unfold this camp**. **Pack up** restores the original workspace; closing the
Camp manager also packs up after a pending change settles. `/xivcamp apply NAME`,
`/xivcamp capture` and `/xivcamp restore` offer the same explicit controls.

Journal, Character, Combat and Chat expose the additive `window.layout` operation
for viewport-relative positions. Camp negotiates each provider's actual methods.
Older modules can still participate through visibility controls while keeping
their positions. Vitals, Inventory, Crafting and the Floating Windows manager
participate through their supported window controls. Missing modules are reported
individually and are never silently installed or enabled.

Existing screen, native and world host preferences are preserved in recovery
state. Camp uses each module's controls; it does not take over a window renderer,
controller lease, Ghostty terminal, native addon or world anchor. Per-world-panel
placements and Rug geometry are later work. It does not move a character, spawn
furniture or start a song. Your existing Piano and Wayfinder continue normally.

Scenes persist across restarts and can be copied/imported as bounded versioned
JSON. Imports do not apply automatically. Before changing a workspace, Camp saves
an original-state journal. Requests wait for provider acknowledgements; refusal,
timeout or provider disappearance starts restoration. Unload queues restoration
and retains recovery until it is acknowledged. If a provider is still unavailable,
**Restore recorded workspace** retries after it returns. No saved recovery is
discarded merely because an older provider cannot restore a position.

A scene can recommend a Prism profile. The paired viewer owns its local display
layout and decides whether to use that profile. Camp works without Prism, Umbra,
Ghostty or a terminal. `Umbra.XivCamp.dll` adds the optional toolbar entry.

## Build

```sh
npm ci --ignore-scripts
npm run build
npm run check
tools/fetch-dalamud.sh
python3 tools/plugin-build.py --test --locked
python3 tools/plugin-package.py
```

.NET SDK 10.0.401, Node 22+ and Python 3 are required. Add
`--umbra-references /path/to/installed/Umbra` for its adapter. Reference hashes are
checked; ZIPs exclude game-provided DLLs and record their source commit.

Core tests cover schema validation, import bounds, older and absent providers,
pre-change persistence, acknowledgement, refusal rollback, timeout and unload
recovery. [Acceptance](docs/ACCEPTANCE.md) records actual live checks; keyboard,
mouse and controller acceptance are distinct. [IPC](docs/IPC.md) describes the
read-only status and queued local UI controls.

The old `preview/` remains an explicitly simulated design demo. Furniture in the
concept artwork is not spawned by this plugin.

[Project](https://spacegho.st/mods/ffxiv/xivcamp/) ·
[Umbra family](https://spacegho.st/mods/ffxiv/umbra/) · [Roadmap](ROADMAP.md).
