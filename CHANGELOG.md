# Changelog

This file records notable changes to Numbat. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/). Every version listed was published to the Rhino Package Manager (Yak) as a pre-release.

## [Unreleased]

### Added
- nbGFAAssign, nbGFACheck and nbGFAHelp: assign uses from the command line, check volumes for problems, and open the nbGFA how-to guide.

### Changed
- nbGFA has been rewritten as a live GFA tracker. Instead of selecting solids and getting one total, it now opens a floating window that updates as you model. Give each storey volume a use, see each use's GFA and share of the total, set minimum and maximum limits per use as a share (%) or an area (m²), and set a minimum and maximum total GFA. Broken limits are flagged; an off-target total shows a small warning icon that explains on hover. Volumes whose use isn't decided yet can go in the default Unassigned use, so they still count toward the total. Uses can be tracked per object or by `GFA::<use>` sublayers, and are saved in the document. See the how-to guide (`nbGFAHelp`).
- nbTemplate: pressing Enter at the placement prompt now uses the origin (0,0,0).
- Numbat now requires Rhino 8.21 or later. It targets .NET 8 (Rhino's default runtime since 8.20) and .NET Framework 4.8, as McNeel recommends.
- Package Manager listing: improved description and search keywords.
- Numbat is now officially Windows-only. Package Manager no longer offers new versions on Mac.

### Fixed
- nbGFA: a floor split into several flat faces is now counted in full. Previously only one of the faces was counted.
- nbGFA: open polysurfaces and volumes without a flat bottom are now flagged in the window instead of being skipped silently.
- The package now includes the .NET Framework build, so Numbat also loads when Rhino 8 is started with `/netfx`. Previously only the .NET Core build was shipped.

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
