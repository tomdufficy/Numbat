# Numbat: notes for Claude Code

Numbat is a Rhino 8 plugin (RhinoCommon, C#) of architectural modelling and analysis commands, all prefixed `nb`. It is distributed through McNeel's Yak package server as `Numbat`. It supports Rhino 8.21 or later, on Windows only.

## Layout

```
src/Numbat/Numbat.slnx               solution
src/Numbat/Numbat/Numbat.csproj      single project: net48 + net8.0 -> Numbat.rhp. <Version> lives here.
src/Numbat/Numbat/manifest.yml       Yak manifest (version injected at build time)
src/Numbat/Numbat/UI/               Numbat's shared Eto UI kit (Kit*, Display): colours, fonts, icons, buttons, dialogs
src/Numbat/Numbat/Commands/
  Analysis/                          nbGFA (live GFA window + nbGFAAssign/Check/Help), nbProgramBlocks
  Modelling/                         nbHandrail, nbPV, nbStair*, nbPavementPlacer, nbRandomColour,
                                     nbTextureShuffle, nbTemplate
scripts/Build-YakPackage.ps1         build + package into dist/*.yak (used by CI and locally)
.github/workflows/ci.yml             build + package on push to main and on every PR; .yak uploaded as an artifact
.github/workflows/release.yml        on tag v*: verify -> test Yak server -> approval -> Yak + GitHub Release
.github/actions/setup-yak/           downloads the pinned Yak CLI (one place to change its version)
.github/dependabot.yml               monthly GitHub Actions updates (NuGet deliberately excluded)
.claude/skills/release/              /release: the release procedure
docs/audit-2026-11.md                audit backlog of known issues per command
docs/nbGFA.md                        nbGFA user guide, embedded in the plugin and shown by nbGFAHelp
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

## Shared UI kit (`src/Numbat/Numbat/UI/`)

New and reworked windows and dialogs use the kit so all of Numbat looks and behaves the same. nbGFA is the first command on it; the others move over as they are reworked.
- Take colours from `Kit.Colours` at paint time (never cache them), so Rhino's light/dark mode and the display options apply live. Don't hard-code colours or fonts in commands.
- Fonts come from `KitFonts`, icons from `KitIcons`, buttons and cards from `KitPaint` / `KitControls`, dialogs from `KitDialog`. Sizes are in design px; `KitCanvas` applies `Kit.Scale`.
- `Display.Load()` applies the saved interface size and outline options; call it before showing a kit window.
- Keep kit types `internal` and free of command-specific code.
- The accent colour is still a placeholder green. It will change to a tan/brown from the new Numbat logo.

nbGFA keeps its user guide in `docs/nbGFA.md`. Update it in the same change as any change to nbGFA's commands, window, menus, units or GFA rules, written for architects (plain steps, no code).

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
