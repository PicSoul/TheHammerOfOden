# Changelog

## 0.1.0

First build, for Valheim 1.0 (Deep North). Everything below works and has been in daily use rather
than only compiled. Single player is well tested; multiplayer is what this build exists to test.

### Shaping a piece

- Pieces rotate on all three axes rather than only on the flat, with a live numeric readout, per-axis
  reset, and an undo that puts everything back at once.
- Pieces can be pushed along your aim, offset on any axis, scaled, and sunk into one another.
- **Bending** curves a piece along a true circular arc, up to half a circle, either way from straight
  — a straight beam becomes an arch, a wall wraps round a tower. It is not two halves hinged at the
  middle, so there is no fold anywhere in it, and a piece keeps its thickness through the curve. The
  piece bends along its longest side, which is measured rather than chosen, and each one has its own
  limit past which the inside of the curve would pass through itself.
- Bent pieces keep their curve when placed, through a zone unloading and through a world reload, and
  their collision is rebuilt as a short chain of boxes that follows the arc. Their snap points travel
  round it too, facing included, so an arch connects the way it looks like it should.
- Only plain structure may be bent: one solid shape, nothing you can use, and a main mesh the game
  lets a mod read. That is measured off each piece rather than read from a list of names, so pieces
  other mods add are judged the same way, and the build menu marks what qualifies with a small arch
  on the icon. Two config lists override it either way.

### Placing it

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
  place with Ctrl and the scroll wheel. Station ranges persist across restarts, and a range another
  player changes is picked up rather than staying stale until the zone reloads.

### Changing it afterwards

- **Editing** takes a built piece back into your hands with its rotation, size and curve already
  loaded, lets every control act on it, and swaps it for the result. The materials move across rather
  than being charged twice. While you work the piece is drawn see-through and stands its collision
  down, so a small nudge is not blocked by the piece's own former self.
- A chest, sign or item stand with something in it is refused rather than quietly emptied, and an
  edited piece comes back at full health.
- Copying a piece takes its curve as well as its angle and size.

### Getting around, and seeing what you are doing

- A build camera detaches and flies, so you can place a piece where you could never have stood to aim
  at it. It carries a light, picks up loose items as it passes while respecting your carry weight, and
  is tethered so the game is never asked to load a second world around it.
- **A reference page** on F3 lists every key the mod uses, read from the settings themselves as they
  stand — so it cannot drift from what the keys actually do, and on a server that enforces its config
  it shows the server's values rather than the ones in your own file.
- A master switch turns the whole mod on and off, and the hammer glows while it is on so the state is
  readable at a glance. Everything but the camera is restricted to the hammer; the camera also works
  with the hoe and cultivator.

### Everything else

- Doors, and only doors, stay usable while a build tool is in hand. Optional auto-open and auto-close
  live together here on purpose, so they cannot fight each other over a door you are standing beside.
  Double doors swing as one.
- `QuietUpgradeGlow` stops an upgraded item's glow shoving the Mistlands mist around as you walk. The
  build camera can also clear mist around itself, with or without a wisplight, detected by status
  effect so any mod's wisplight counts.
- The settings that decide what the world allows — reach, scale and bend limits, station ranges, how
  much one zoop may place, whether undo hands the materials back — are the server's to set when it
  runs the mod. Keys, colours and camera feel stay yours. Single player is untouched by any of it.
- Three diagnostic keys are included and switched off by default, so F9 to F11 stay free unless you
  turn debugging on.

### Known limits

- **The mod is required on the server and on every client.** A piece's curve and its size are both
  read back by this mod and by nothing in the base game, so a player without it would see a straight,
  unscaled version of what you built — collider included. The server turns away a client that does not
  have it, or that has a different version.
- Some pieces cannot be bent however plain they look, because their meshes were shipped without the
  read access a mod needs. The darkwood roofs are the notable case. The build menu's arch says which.
- While a piece is bent, any part of it too coarse to follow a curve is left undrawn, as is any
  part whose mesh the game will not let a mod read. A deformer can only move vertices that exist,
  and a plain box has none between its corners - bending one lifts the corners onto the arc and
  leaves flat faces spanning between, which draws as a straight bar across the arch. Everything
  else is cut finer first so that it curves rather than folds, and bent pieces are also held at
  full detail rather than dropping to a distant stand-in. Unbent pieces are untouched by all of
  this.
