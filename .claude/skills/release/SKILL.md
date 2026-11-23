---
name: release
description: Cut a new Numbat release. Picks the version, updates CHANGELOG.md and Numbat.csproj, and tags it. CI then publishes to the test Yak server, waits for the maintainer to test and approve, and publishes to Yak and GitHub Releases. Use when the maintainer asks to release, publish or ship a version.
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

The Release workflow runs three jobs:
1. **build** checks the tag against the csproj version, checks that the commit is on main and that the changelog section exists, then builds and packages.
2. **test-publish** pushes to the test Yak server (https://test.yak.rhino3d.com, wiped nightly). The run summary has the testing instructions.
3. **publish** waits for the maintainer to approve the `yak` environment on GitHub, then pushes to the real Yak server and creates the GitHub Release.

## 5. Hand over for testing and approval

Once test-publish has succeeded, give the maintainer:
- the run link
- the steps to test from the test server:
  1. In Rhino, open `Options`, then Package Manager, and add the source `https://test.yak.rhino3d.com`.
  2. Install the version with "Include pre-releases" ticked.
  3. Restart Rhino and try the commands.
  4. Remove the test source again.
- how to approve or reject: on the run page, click "Review deployments", tick `yak`, then choose Approve and deploy, or Reject.

Never approve on the maintainer's behalf.

**If rejected:** nothing reached the real Yak server, so the version number is not used up. Fix the problem through a normal PR. Then, with the maintainer's go-ahead, move the tag to the fixed commit and push it again. This starts a new run:
```powershell
git tag -d v<version>
git push origin :refs/tags/v<version>
git tag -a v<version> -m "Numbat v<version>"
git push origin v<version>
```

## 6. Confirm

- The run finished green: `gh run list --workflow Release -L 1`.
- `yak search --all --prerelease Numbat` lists the new version.
- `gh release view v<version>` shows the release.
- Report back with the links.
