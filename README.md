<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/brand/numbat-icon-tile.svg">
    <img src="assets/brand/numbat-icon.svg" width="160" alt="Numbat logo">
  </picture>
</p>

# Numbat

Architectural modelling and analysis tools for Rhino 8. Every command starts with `nb`, so typing `nb` in Rhino lists them all.

| Command | What it does |
|---|---|
| [`nbGFA`](#nbgfa) | Live gross floor area tracking by use, with limits and a total target. |
| [`nbTextureShuffle`](#nbtextureshuffle) | Breaks up repeating textures by shuffling their mapping. |
| [`nbHandrail`](#nbhandrail) | Configurable handrails with rails, posts, infill and panels. |
| [`nbPavementPlacer`](#nbpavementplacer) | Arrays a paving module along curves. |
| [`nbRandomColour`](#nbrandomcolour) | Randomises layer colours. |

## Installation

Requires Rhino 8.21 or later on Windows.

In Rhino, open the **Package Manager**, tick **Include pre-releases**, search for **Numbat** and click **Install**. What changed in each version is in the [changelog](CHANGELOG.md).

## nbGFA

Tracks gross floor area while you model. Model each storey as one closed box and give it a use (Residential, Office, ..., or Unassigned while you decide). A floating window shows each use's GFA and share of the total, flags uses outside their limits (% or m²), and warns when the total is off its target. GFA is reported in m².

The full guide is in [docs/nbGFA.md](docs/nbGFA.md), and in Rhino behind the window's **?** button.

Everything is done from the window. These helper commands are for macros and keyboard users. They don't appear as you type, but work when typed in full:

| Command | What it does |
|---|---|
| `nbGFAAssign` | Select volumes, then pick a use (or Exclude) on the command line. |
| `nbGFACheck` | Checks assigned volumes: open, tilted, no flat bottom, overlapping. |
| `nbGFAHelp` | Opens the guide. |
| `nbGFADemo` | Adds three example blocks (2, 4 and 6 storeys) to try nbGFA on. |

## nbTextureShuffle

Breaks up obviously repeating textures such as brick, stone or timber cladding. Select surfaces, polysurfaces, meshes or SubDs, open the window and click **Shuffle** as often as you like. Each click moves their box mapping by a new random amount and is one undo step.

- **Repeat X / Y / Z**: the size of one texture repeat. Changing it resizes the selection's mapping straight away.
- **Align to**: World keeps neighbouring objects continuous; Object follows each object's largest flat face.
- **Same for all selected**: every selected object moves by the same amount; off, each gets its own.
- **Shift up to / along**: how far, and in which directions, the texture may move.
- **Straighten** puts the mapping back at its starting position. **Remove mapping** returns to Rhino's default mapping.

For macros, `-nbTextureShuffle` shuffles the selected objects once with the window's saved settings.

## nbHandrail

Builds a handrail along points you pick. Options include top and bottom rails, posts, vertical or zig-zag infill, framed panels, sheets, end tabs and support feet.

<p align="center">
  <img src="assets/demos/nbHandrail.gif" width="900" alt="nbHandrail: picking points and adjusting the handrail options">
</p>

## nbPavementPlacer

Arrays a paving module along curves, with options for rows, gaps, rotation, scale variation, side and start point.

<p align="center">
  <img src="assets/demos/nbPavementPlacer.gif" width="640" alt="nbPavementPlacer: placing pavers along a curve and adjusting the options">
</p>

## nbRandomColour

Randomises layer colours, either per layer or per top-level layer group, with lighter shades for the layers nested inside.

## Commands in rework

nbStair, nbStairStraight, nbStairSpiral and nbPV are withdrawn for now while they are rebuilt to a more professional standard. They will return in a later release. nbProgramBlocks and nbTemplate have been retired.

## Building from source

Requires the .NET 8 SDK on Windows.

```powershell
dotnet build src/Numbat/Numbat/Numbat.csproj -c Release   # build the plug-in
./scripts/Build-YakPackage.ps1                            # build and package into dist/*.yak
```

To try a build, drag `Numbat.rhp` from `bin/` into Rhino. Debug builds also contain the commands in rework. Conventions, CI and the release process are described in [CLAUDE.md](CLAUDE.md).

## License

MIT. See [LICENSE](LICENSE).
