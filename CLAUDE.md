# Numbat: notes for Claude Code

Numbat is a Rhino 8 plugin (RhinoCommon, C#) of architectural modelling and analysis commands, all prefixed `nb`. It is distributed through McNeel's Yak package server as `Numbat`. It supports Rhino 8.21 or later, on Windows only.

## Layout

```
src/Numbat/Numbat.slnx               solution
src/Numbat/Numbat/Numbat.csproj      single project: net48 + net8.0 -> Numbat.rhp. <Version> lives here.
src/Numbat/Numbat/manifest.yml       Yak manifest (version injected at build time)
src/Numbat/Numbat/UI/               Numbat's shared Eto UI kit (Kit*, Display): colours, fonts, icons, buttons, dialogs
src/Numbat/Numbat/Commands/
  Analysis/                          nbGFA (live GFA window + nbGFAAssign/Check/Help), nbAF (Abstandsflächen window + helpers)
  Modelling/                         nbHandrail, nbPavementPlacer, nbRandomColour, nbTextureShuffle
  Preview/                           in rework, Debug builds only: nbStair*, nbPV (see "Preview commands")
scripts/Build-YakPackage.ps1         build + package into dist/*.yak (used by CI and locally)
.github/workflows/ci.yml             build + package on push to main and on every PR; .yak uploaded as an artifact
.github/workflows/release.yml        on tag v*: verify -> test Yak server -> approval -> Yak + GitHub Release
.github/actions/setup-yak/           downloads the pinned Yak CLI (one place to change its version)
.github/dependabot.yml               monthly GitHub Actions updates (NuGet deliberately excluded)
.claude/skills/release/              /release: the release procedure
docs/audit-2026-11.md                audit backlog of known issues per command
docs/nbGFA.md                        nbGFA user guide, embedded in the plugin and shown by nbGFAHelp
docs/nbAF.md                         nbAF user guide, embedded in the plugin and shown by nbAFHelp
docs/sources/                        legal texts behind nbAF's rules (PDFs ship with the plugin) and the rules table
CHANGELOG.md                         Keep a Changelog; [Unreleased] is always current
```

Larger commands each get a folder and follow this split:
- `Nb<Name>Command.cs` handles the Rhino command, prompts and options.
- `*Parameters` / `*Settings` holds user inputs.
- `*Solver` does the pure maths and validation.
- `*Builder` creates geometry.
- `*Geometry` / `*Solution` are the result containers.
- `*PreviewConduit` draws the live preview.
- `*Bake` adds objects, layers and blocks to the document.

Keep new commands in this shape.

Each tool has one discoverable command (`nbGFA`, `nbTextureShuffle`, ...). Helper commands that a window already covers (`nbGFAAssign`, `nbGFACheck`, `nbGFAHelp`) get `[CommandStyle(Style.Hidden)]`: they work when typed in full but stay out of autocomplete, so typing `nb` only lists the main tools.

**Demo commands are optional.** When developing or reworking a tool, suggest (don't add unasked) a hidden `<command>Demo` (for example `nbGFADemo`) if a ready-made test case would help users try it. When the maintainer wants one, it ships in every build, is `[CommandStyle(Style.Hidden)]`, builds simple example geometry on a layer named `<command> demo`, replaces only that layer's contents when run again, works in any metric unit, zooms to the result and prints which command to run next. Mention it in the tool's README section and user guide.

## Shared UI kit (`src/Numbat/Numbat/UI/`)

New and reworked windows and dialogs use the kit so all of Numbat looks and behaves the same. nbGFA, nbAF and nbTextureShuffle are on it; the others move over as they are reworked, and new commands start on it.
- Take colours from `Kit.Colours` at paint time (never cache them), so Rhino's light/dark mode and the display options apply live. Don't hard-code colours or fonts in commands.
- Fonts come from `KitFonts`, icons from `KitIcons`, buttons and cards from `KitPaint` / `KitControls`, dialogs from `KitDialog`. Sizes are in design px; `KitCanvas` applies `Kit.Scale`.
- `Display.Load()` applies the saved interface size and outline options; call it before showing a kit window.
- Every window derives from `KitWindow` and every dialog from `KitDialog`. They replace the system title bar with `KitTitleBar`: logo brown (#6C4B33) in light and dark mode, white text, 36 px, drag to move. Its text, and the window's `Title`, is always `<command> – <full name>`, for example `nbGFA – Gross Floor Area` or `nbAF – Abstandsflächen` (German names keep their umlauts). Guides are `<that title> · Guide`; dialogs are `<command> – <what it edits>`.
- Window-level buttons go in the title bar, right-aligned before the close button, in this order: view toggles, ? (guide), ⋯ (menu). Don't paint another header inside the content. No mark, logo or icon in the title bar until commands get their own icons.
- No how-to or instruction text in window footers. A footer holds status (for example limits met / broken) and the copyright only. Explain how things work in hover tips, the empty state of a list, or the user guide.
- Keep kit types `internal` and free of command-specific code.
- The accent comes from the Numbat logo palette (ink #2D2B28, tan #C79C69, cream #FFFBEA, brown #6C4B33, rust #A9714B): brown in light mode, tan in dark mode. Tan and rust are too light for text on light backgrounds. Keep every text/background pair at 4.5:1 contrast or better.

nbGFA keeps its user guide in `docs/nbGFA.md`. Update it in the same change as any change to nbGFA's commands, window, menus, units or GFA rules, written for architects (plain steps, no code).

Guides are shown in Rhino by `KitHelpWindow`, which renders a small Markdown subset (`#`, `##`, `- ` bullets, paragraphs). Keep guides to that subset; tables belong in other docs. Prefix every per-user plugin setting of a command with its own prefix (`af.` for nbAF) so commands don't share window positions or colours.

## nbAF (`Commands/Analysis/NumbatAf/`)

Abstandsflächen to HBauO § 6 (Hamburg, 2025 text). nbAF and nbGFA expect the same geometry: stacked closed volumes, one per storey or part; roofs may be separate volumes.
- `AfModel` holds the live state. The truth lives on the objects: tracked volumes carry `numbat.af.role` (target/context) and `numbat.af.plot` user strings. Rule values are doc strings (section `numbat.af`), colours are per-user settings. One target at most; areas for all buildings, clashes only where the target is involved.
- Recalculation runs only while the window is open, and only reads the document. `AfPreviewConduit` draws the result; `nbAFBake` writes it to `nbAF::` layers (one undo step), `nbAFClear` deletes it. Don't write output from idle or window events.
- Output is one closed curve per building face (`AfFace`: all walls of a building facing the same way, areas unioned and clipped to outside the own footprint), never per wall or as surfaces. The checks run on these face regions. Clashes are numbered markers plus the window's list; depth labels are opt-in (`AfColours.ShowLabels`), drawn as plan text sized to fit each wall's area (`AfSolver.Labels`: deepest first, colliding or covered ones dropped, hidden on screen below 8 px).
- `AfBuildings` (volumes to buildings, no boolean union), `AfWalls` (façades, H, area outlines per wall), `AfSolver` (faces, checks), `AfExporter` (DXF R12 / SVG / JPG), `AfRules`, `AfSources`, `AfColours`.
- Rule values vary between Landesbauordnungen: keep them settings in `AfRules`, never constants, and name where a default comes from. Keep the rules table in `docs/sources/README.md` and the guide `docs/nbAF.md` up to date in the same change as any change to the rules, checks, window or export. Every source has a saved copy (where it may be redistributed) and its date in both `docs/sources/README.md` and `AfSources.cs`.
- Test every change against `nbAFDemo`. Expected with the HBauO defaults: 19 volumes, 4 buildings: B1 = A, B2 = D, B3 = B (target), B4 = C.
  - A (2 storeys + 35° roof volume): eaves sides H 7.80 = 6.40 + 4.20/3, depth 3.12. Gable ends depth 2.56–4.24.
  - B (4 storeys): depth 5.12. C (6 storeys): depth 7.68.
  - D (stepped): depths 7.68 (H 19.20), 6.40 (H 16.00) and 3.84 (H 9.60).
  - 3 clashes, all B3/B4: 2 Overlap B3/B4 (17.7 and 2.5 m²) and 1 "B4 on plot of B3" (26.8 m²). B3's 5.12 m areas leave its 4 m plot on all sides but reach no other plot, so they are not flagged.

## Preview commands (`Commands/Preview/`)

Commands being reworked live in `src/Numbat/Numbat/Commands/Preview/`. The csproj compiles that folder only in Debug builds, so they can be developed and tested in Rhino (Debug build, launch profile or dragged-in `bin/Debug/.../Numbat.rhp`) but are absent from Release builds and the Yak package. CI builds Debug too, so they keep compiling.
- Don't list preview commands in the README command list. The README's "Commands in rework" section names them.
- No CHANGELOG entry for work on a preview command, except when it is withdrawn or returns.
- To release one: `git mv` its folder back under `Commands/Modelling` or `Commands/Analysis`, add its README section and demo, and add an "Added" (or "Changed") CHANGELOG entry saying it is back. Before 1.0 that is a minor bump.
- Their demo GIFs stay in `assets/demos/` meanwhile.

## Build and package

```powershell
dotnet build src/Numbat/Numbat/Numbat.csproj -c Release
./scripts/Build-YakPackage.ps1     # -> dist/numbat-<version>-rh8_21-win.yak
```

The build must stay at zero warnings. The package holds `net48/` and `net8.0/` subfolders with `manifest.yml` at the root, which is McNeel's multi-target layout. The RhinoCommon package version sets the Yak distribution tag (`rh8_21`), and therefore the minimum Rhino version. Only raise it deliberately, and say so in the changelog. The platform tag is `win` because Numbat is officially Windows-only. Keeping the code free of Windows-only APIs still matters, in case Mac support is added later.

## Testing in Rhino

There are no automated tests yet. To verify a change:
1. Build Debug, then run the "Rhino 8 - netcore" launch profile (`Properties/launchSettings.json`), or drag `Numbat.rhp` into Rhino 8. To test the packaged build, drag the `.yak` from `dist/` or from a CI artifact into Rhino.
2. If the Rhino MCP tools (`mcp__Rhino_MCP_Platform__*`) are available, use them to run the command (`run_command`), inspect the objects created (`list_objects`) and take a viewport screenshot (`get_viewport_image`).
3. Test in a mm model and in a metres model. Unit handling is the most common bug class here.

## Rhino / McNeel conventions

- Use the `RhinoDoc doc` passed to `RunCommand`, never `RhinoDoc.ActiveDoc`.
- Make every document change inside `RunCommand` so Undo works. Don't add geometry from a modeless form's Closed event after the command has returned. Use `ShowSemiModal(doc, RhinoEtoApp.MainWindow)` or `ShowModal(RhinoEtoApp.MainWindow)` for Eto dialogs.
- Disable display conduits in a `finally` block so cancel or exception never leaves a preview stuck on.
- Never hardcode millimetre values. Define defaults in mm and scale them with `RhinoMath.UnitScale(UnitSystem.Millimeters, doc.ModelUnitSystem)`. Report lengths and areas with their units.
- Use `doc.ModelAbsoluteTolerance` / `doc.ModelAngleToleranceRadians` for tolerances.
- Validate layer names with `Layer.IsValidName`. Check `doc.Layers.Add` and `doc.InstanceDefinitions.Add` for a `-1` return. Don't overwrite the properties of existing layers.
- Use Eto for UI and clipboard access, not WinForms. Numbat ships Windows-only, but Eto keeps the Mac option open. `System.Drawing.Color` is fine.
- The code must compile for net48: no APIs newer than .NET Framework 4.8.
- Where practical, give dialog-driven commands a scripted fallback (`RunMode.Scripted` uses command-line options).

API reference, checked locally first:
- RhinoCommon XML docs: `~/.nuget/packages/rhinocommon/<version>/lib/net48/RhinoCommon.xml`, with `Eto.xml` and `Rhino.UI` in the same folder. Grep it for signatures and remarks.
- Guides: https://developer.rhino3d.com/guides/rhinocommon/
- API: https://developer.rhino3d.com/api/rhinocommon/
- Eto: https://developer.rhino3d.com/guides/eto/
- Yak: https://developer.rhino3d.com/guides/yak/ (the CLI reference and multi-targeted packages pages)
- Target frameworks: https://developer.rhino3d.com/guides/rhinocommon/moving-to-dotnet-core/
- Samples: https://github.com/mcneel/rhino-developer-samples

## Repo, CI and release best practice

The setup that predates Claude Code was not presumed correct. When touching repo structure, CI or release tooling, follow current official guidance over what is already there, and say so when changing it.
- GitHub Actions: https://docs.github.com/en/actions
  - Keep `permissions:` minimal.
  - Secrets: `YAK_TOKEN`, a non-expiring key from `yak login --ci`. It is stored only in the `yak-test` and `yak` environments, both limited to `v*` tags, never as a repo-level secret.
  - The `yak` environment requires the maintainer's approval. Never weaken or bypass that.
  - Security hardening: https://docs.github.com/en/actions/security-for-github-actions/security-guides/security-hardening-for-github-actions
- GitHub Releases: https://docs.github.com/en/repositories/releasing-projects-on-github
- Dependabot: https://docs.github.com/en/code-security/dependabot/working-with-dependabot/keeping-your-actions-up-to-date-with-dependabot
- Conventional Commits: https://www.conventionalcommits.org/ · Keep a Changelog: https://keepachangelog.com/ · SemVer: https://semver.org/

When unsure what current guidance says, fetch the page rather than relying on memory.

## Git workflow

- `main` is always buildable and is the only long-lived branch.
- Do work on short branches (`feat/nbpv-facing`, `fix/nbhandrail-units`, `chore/...`) and merge them through a PR once CI is green. Squash merges keep main's history to one line per change.
- Use Conventional Commits, scoped to the command where relevant: `feat(nbStair): add landing depth option`, `fix(nbHandrail): scale defaults to model units`, `docs:`, `ci:`, `refactor:`, `chore(release): v0.4.0-beta.1`.
- In the same commit or PR as any user-visible change:
  - add an entry under `## [Unreleased]` in `CHANGELOG.md`, written for users
  - update the command's README section if its behaviour, inputs or limitations changed. New commands need a README section and an "Added" entry.
  - tick off the item in `docs/audit-2026-11.md` if the change resolves one.
- Never push, merge, tag or publish without an explicit go-ahead.

## Be proactive about commits, versions and releases

The maintainer wants Claude to keep track of this without being asked:
- At the end of a coherent unit of work, suggest a commit with a proposed message. Don't let unrelated changes pile up uncommitted.
- When making a change, say which SemVer bump it implies and keep a running view of the next version, based on `[Unreleased]`.
- Say when it's a good time to open or merge a PR or to cut a release (a finished feature, a batch of fixes worth shipping), and when it isn't (half-finished work, an untested build).

## Versioning

SemVer. `<Version>` in `Numbat.csproj` is the single source of truth, and the tag must be `v<Version>`. CI enforces this.
- Before 1.0, bump the minor for a new command, a new feature or a breaking change, and the patch for fixes only.
- From 1.0, standard SemVer applies: major for breaking changes, minor for features, patch for fixes.
- Pre-releases are `-beta.N`. Increment N for each publish of the same base version, and restart at `-beta.1` for a new base version. Users need "Include pre-releases" to see them.
- Changes that only touch CI, docs or the repo don't need a release on their own. They ship with the next one.
- A Yak version can never be overwritten or re-pushed, so every publish needs a new version.
- To release, use `/release` (`.claude/skills/release/SKILL.md`).
