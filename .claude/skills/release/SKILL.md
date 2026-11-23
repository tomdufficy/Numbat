---
name: release
description: Cut a new Numbat release. Picks the version, updates CHANGELOG.md and Numbat.csproj, commits, then tags and pushes so CI publishes to Yak and GitHub Releases. Use when the maintainer asks to release, publish or ship a version.
disable-model-invocation: true
argument-hint: "[version, e.g. 0.4.0-beta.1 (optional)]"
---

# Release Numbat

Publishing is irreversible: a Yak version can never be overwritten or re-pushed. Stop and ask whenever something is unclear.

## 1. Preflight (all must pass)

- `git switch main` and `git pull --ff-only`. The tree must be clean (`git status --porcelain` is empty).
- The latest CI run on main is green: `gh run list --workflow CI --branch main -L 1`.
- `CHANGELOG.md` has entries under `## [Unreleased]`. If it is empty, there is nothing to release, so stop.
- Run `pwsh ./scripts/Build-YakPackage.ps1` locally. It must succeed.

## 2. Choose the version

Read the current `<Version>` in `src/Numbat/Numbat/Numbat.csproj` and the `[Unreleased]` entries. Apply SemVer:

| Unreleased contains | Before 1.0 | From 1.0 |
|---|---|---|
| Removed command or breaking option/behaviour change | minor | major |
| New command or feature (`Added`) | minor | minor |
| Only fixes or small changes | patch | patch |

- Pre-releases: continuing the same base version increments `-beta.N`. A new base version starts at `-beta.1`. A stable release drops the suffix.
- If the user passed a version in `$ARGUMENTS`, use it, but warn if it contradicts the table.
- The tag `v<version>` must not already exist locally or on origin (`git ls-remote --tags origin v<version>`).

Present the proposed version with a one-line justification and the changelog entries. Wait for confirmation.

## 3. Prepare the release commit (on a branch, via PR)

1. Create the branch `release/v<version>`.
2. In `CHANGELOG.md`:
   - Rename `## [Unreleased]` to `## [<version>] - <YYYY-MM-DD>` and add a fresh empty `## [Unreleased]` above it.
   - Update the compare links at the bottom: `[Unreleased]` compares `v<version>...main`, and add `[<version>]` comparing the previous tag with `v<version>`.
3. Set `<Version>` in `Numbat.csproj`.
4. Make sure the README reflects every command change in this release.
5. Commit `chore(release): v<version>`, push, and open a PR. When CI is green and the maintainer approves, merge it (squash).

## 4. Tag (only after an explicit go-ahead)

```powershell
git switch main
git pull --ff-only
git tag -a v<version> -m "Numbat v<version>"
git push origin v<version>
```

The Release workflow checks the tag against the csproj version, checks that the commit is on main and that the changelog section exists, then pushes to Yak and creates the GitHub Release.

## 5. Confirm

- Check the result with `gh run watch` (or `gh run list --workflow Release -L 1`).
- `yak search --all --prerelease Numbat` lists the new version.
- `gh release view v<version>` shows it.
- Report back with the links.
