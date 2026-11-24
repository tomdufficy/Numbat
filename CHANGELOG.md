# Changelog

This file records notable changes to Numbat. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/). Every version listed was published to the Rhino Package Manager (Yak) as a pre-release.

## [Unreleased]

### Added
- nbAnon: finds a word or phrase (your company's or a client's name) everywhere in a Rhino file, including layers, nested blocks, object names and user text, text, groups, document notes, named views, styles and materials, and replaces it with a random code, a random word, your own text or nothing. Review and untick every place first; all changes are one undo step, and the list can be saved as a report.
- nbAF: live Abstandsflächen (setback areas) for massing models, to the Hamburg building code (HBauO § 6, 2025 text). Add stacked storey volumes in a floating window and, optionally, give buildings their plots. Each building face gets one Abstandsfläche, stepped walls included, drawn as a live preview. Every building is checked against every other (overlaps, areas reaching into another building, areas reaching another plot, and areas past a road centre line once you add them), filled in the clash colour, numbered in the viewports and listed in the window, where hovering one says which rule it breaks, with the numbers. Depth dimensions can be turned on. A model check runs with every update: volumes closer than 10 mm count as one building, buildings that almost touch are listed for you to join or keep apart, open objects and leaning walls are reported, and Colour by building shows what nbAF counts as one building. Every rule value (depth factor, minimum depth, roof pitch, GE/GI zone) can be changed and is saved in the file. Bake adds the result to `nbAF::` layers as closed curves; Export saves a 2D top view as DXF, SVG or JPG in a fixed drawing style, with optional clash numbers, notes and dimensions. The guide, and saved copies of the legal texts, are behind the window's ? button. Helper commands for macros: nbAFAdd, nbAFSetPlot, nbAFAddRoad, nbAFBake, nbAFClear, nbAFSources, nbAFHelp and nbAFDemo (an example district from a real hand-made model).
- nbGFA helper commands, for macros and keyboard users: nbGFAAssign (assign a use on the command line), nbGFACheck (check volumes), nbGFAHelp (open the guide) and nbGFADemo (example blocks to try nbGFA on). They work when typed in full but don't appear as you type.

### Changed
- The sections of the nbGFA, nbAF and nbAnon windows open and close with an arrow before each heading, and stay as you left them. nbAnon also gets the same header as the other windows, with ? (guide) and ⋯ (menu).
- Numbat windows and dialogs show Numbat's "nb" mark and the tool's full name in their title bar, for example "nbGFA – Gross Floor Area". This applies to nbGFA, nbAF, nbTextureShuffle, the guides and every dialog.
- nbGFA is now a live GFA tracker. Instead of selecting solids and getting one total, it opens a floating window that updates as you model:
  - Each storey is one closed box; GFA is the area of its flat bottom, and floors split into several faces count in full.
  - Give volumes a use and see each use's GFA and share of the total, as bars or a pie chart. Unassigned is a default use for volumes that count but whose use isn't decided yet.
  - Limit each use by share (%) or area (m²), and set a minimum and maximum total GFA. Anything off is flagged. Hovering a use, the status line or the volume issues box gives the numbers: how far over or under a limit, in % and m², and how many volumes have which problem.
  - Open, tilted or flat-bottom-less volumes are flagged instead of being skipped silently.
  - Track uses on each object or by `nbGFA::<use>` sublayers. All settings are saved in the file. The guide is behind the window's ? button.
- nbTextureShuffle now works. It opens a small window: select objects and click Shuffle as often as you like to move their box texture mapping by a new random amount (one undo step per click). Straighten puts the mapping back at its start; Remove mapping takes it off.
- Numbat now requires Rhino 8.21 or later, and is officially Windows-only.
- New Package Manager icon, description and search keywords.

### Removed
- nbProgramBlocks and nbTemplate.
- nbStair, nbStairStraight, nbStairSpiral and nbPV, temporarily, while they are rebuilt. They will return in a later release.

### Fixed
- nbHandrail: the panel frame options were labelled the wrong way round. They now read "Single Solid" and "Mitred" and each builds what it says. Saved choices keep producing the same frame as before.
- Numbat now also loads when Rhino 8 is started with `/netfx`.

## [0.3.0-beta.2] - 2026-08-20

### Added
- nbProgramBlocks: creates area-scaled program blocks from tab-separated area schedules copied from Excel.

### Changed
- nbHandrail: settings now open in a dialog window, and the handrail generation logic has been reworked.
- README: demo GIFs added for nbHandrail, nbPavementPlacer, nbStairStraight and nbStairSpiral.

## [0.3.0-beta.1] - 2026-07-09

### Added
- nbPV: photovoltaic panel arrays on roof geometry.
- nbStair, nbStairStraight and nbStairSpiral: straight stairs (landings, switchbacks, stringers) and spiral stairs, both with preview-and-bake.
- nbTemplate: recreates the Numbat template layer structure with labelled reference geometry.
- nbHandrail: panel infill with framed panels and sheets, end tabs, and preview dimensions.

### Changed
- nbHandrail: railing logic simplified.

## [0.2.0-beta.1] - 2026-07-06

### Added
- nbHandrail: handrail generator with rails, posts and vertical or zig-zag infill.
- nbPavementPlacer: arrays a paving module along curves.
- nbTextureShuffle: collects options for texture-mapping shuffle. It does not modify objects yet.

### Changed
- The repository was restructured, and release publishing now goes through GitHub Actions to Yak.

## [0.1.0-beta.3] - 2026-06-10

### Changed
- README updates.

## [0.1.0-beta.2] - 2026-06-10

### Added
- nbGFA: gross floor area from closed polysurfaces.
- nbRandomColour: randomises layer colours.
- Yak package metadata and automated packaging workflow.

[Unreleased]: https://github.com/tomdufficy/Numbat/compare/v0.3.0-beta.2...main
[0.3.0-beta.2]: https://github.com/tomdufficy/Numbat/compare/v0.3.0-beta.1...v0.3.0-beta.2
[0.3.0-beta.1]: https://github.com/tomdufficy/Numbat/compare/v0.2.0-beta.1...v0.3.0-beta.1
[0.2.0-beta.1]: https://github.com/tomdufficy/Numbat/compare/v0.1.0-beta.3...v0.2.0-beta.1
[0.1.0-beta.3]: https://github.com/tomdufficy/Numbat/compare/v0.1.0-beta.2...v0.1.0-beta.3
[0.1.0-beta.2]: https://github.com/tomdufficy/Numbat/releases/tag/v0.1.0-beta.2
