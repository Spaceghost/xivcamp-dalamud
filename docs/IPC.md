# Versioned local interfaces

The public JSON call gate is `XivCamp.v1.Call`. `api.version` reports version
and methods; unknown methods are refused. `status` reads cached, bounded state.
UI mutations are queued; they do not run inside an IPC callback. The optional
Umbra adapter uses `XivCamp.Toggle`, `.OpenSettings`, and `.Ready`. Provider
assembly references never cross these boundaries.

`window.open`, `window.close`, `scene.apply` (a saved scene name) and
`scene.restore` are supported. Observe `busy`, `phase`, `activeScene`, `reports`
and `recoveryPanels` in status until the change settles. The allowlist contains
UI operations only; no slash command, combat action, chat send, teleport or music
playback operation can be imported in a scene. Every provider request is polled
for its own acknowledgement. Original state is saved before issuing requests.
