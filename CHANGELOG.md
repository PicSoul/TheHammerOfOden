# Changelog

## 0.1.0

First build, for Valheim 1.0 (Deep North). Everything below works and has been in daily use rather
than only compiled. Single player is well tested; multiplayer is what this build exists to test.

- Pieces rotate on all three axes rather than only on the flat, with a live numeric readout, per-axis
  reset, and an undo that puts everything back at once.
- Pieces can be pushed along your aim, offset on any axis, scaled, and sunk into one another.
- Snap points are named and drawn where they actually are, so you can see what a piece will attach to
  before you commit. The anchor you last used for a kind of piece is remembered, and survives a
  restart.
- Surface placement lays a piece flat against whatever you are pointing at, taking its angle from the
  surface rather than from the ground.
- Freeze pins the ghost in the air so you can walk around a piece before placing it, and nudge moves
  it on any axis while it is pinned.
- Grid snapping, with an adjustable step.
- Zooping lays a whole run of pieces in one action, along one axis or two, spaced by the piece's own
  size. Placement is spread across frames so a long run does not stutter.
- Undo takes back whole zoops rather than single pieces, refunds the materials into your inventory,
  and respects both your free slots and your carry weight — anything that will not fit is dropped at
  your feet rather than lost.
- Placement reaches as far as the nearest workbench's build range, and that range is adjustable in
  place with Ctrl and the scroll wheel. Station ranges persist across restarts.
- A build camera detaches and flies, so you can place a piece where you could never have stood to aim
  at it. It carries a light, picks up loose items as it passes while respecting your carry weight, and
  is tethered so the game is never asked to load a second world around it.
- A master switch turns the whole mod on and off, and the hammer glows while it is on so the state is
  readable at a glance. Everything but the camera is restricted to the hammer; the camera also works
  with the hoe and cultivator.
- Doors, and only doors, stay usable while a build tool is in hand. Optional auto-open and auto-close
  live together here on purpose, so they cannot fight each other over a door you are standing beside.
  Double doors swing as one.
- `QuietUpgradeGlow` stops an upgraded item's glow shoving the Mistlands mist around as you walk. The
  build camera can also clear mist around itself, with or without a wisplight, detected by status
  effect so any mod's wisplight counts.
