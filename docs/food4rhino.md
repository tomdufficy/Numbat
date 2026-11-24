# Food4Rhino page

Numbat's page: https://www.food4rhino.com/en/app/numbat. This file records what is on it, so each release can be checked against it. The maintainer edits the page by hand (Food4Rhino > My Content > Numbat > Edit); McNeel reviews changes, usually within 1–2 days.

## What updates by itself, and what doesn't

- The page is linked to the Numbat package on the Rhino Package Manager. Its install button opens Rhino's Package Manager, so every release is installable from the page without touching it.
- Everything else on the page is entered by hand and stays until it is changed there: title, short description, body, icon, images, category, licence, support email, website and platform. Food4Rhino does not read the package's description, icon or keywords (`src/Numbat/Numbat/manifest.yml`); those only feed the listing inside Rhino's Package Manager.
- Don't use Food4Rhino's "Push" button: releases go to the Package Manager through the release workflow only.

## When the page needs updating

Checked at every release (step 3 of `.claude/skills/release/SKILL.md`). Compare the release with the record below:

- **Body:** a command added, removed, withdrawn, returned or renamed, or what a command does changed (README command table and sections), or the requirements changed (Rhino version, Windows only, beta status and "Include pre-releases").
- **Short description:** the same kinds of change, if they affect the one-line summary. At most 180 characters.
- **Project Icon:** `assets/brand/numbat-icon-512.png` changed since the version recorded below (`git log -1 --format=%h -- assets/brand/numbat-icon-512.png` differs from the recorded commit).
- **Platform:** Numbat supports Mac again, or stops supporting Windows.
- **Images:** only when new or better demo images for the README have been added to `assets/demos/` (or new diagrams to `docs/images/`): offer them as replacements for the page's images. No reminder otherwise; the current images are fine as placeholders.
- Category, licence, support email and website: only if they change (LICENSE, repository URL).

When something needs updating, give the maintainer the new text for each field (short description checked against 180 characters, body with the formatting described below) and a zip of any files to upload, then update this record in the same PR or right after.

## Current page (since 0.4.0-beta.1, November 2026)

### Title

Numbat

### Short description

Architectural tools for Rhino 8: live gross floor area by use, Abstandsflächen (setback areas) with clash checks, name anonymising, handrails, paving and texture shuffling.

### Body

Food4Rhino's editor has bold, italic, bullet lists, numbered lists and links. Headings are bold lines, tools are bullets with the command name in bold, install steps are a numbered list, and "Numbat GitHub repository" links to https://github.com/tomdufficy/Numbat. No italics.

Numbat adds architectural modelling and analysis tools to Rhino 8. It takes care of small, repeatable production tasks that would otherwise need manual setup or repeated modelling. Every command starts with nb, so typing nb in Rhino lists them all.

**Analysis**

- **nbGFA** tracks gross floor area live while you model. Model each storey as a closed box and give it a use: a floating window shows each use's area and share of the total, as bars or a pie chart, with limits per use and a target for the total.
- **nbAF** draws the Abstandsflächen (setback areas) of a massing model to the Hamburg building code (HBauO § 6), as a live preview. Every building is checked against every other, against plot lines and against road centre lines; clashes are numbered and explained, with the numbers. Every rule value can be changed. Bake the result as curves, or export it as DXF, SVG or JPG. The guide explains the basic principles with diagrams.

**Utilities**

- **nbAnon** finds names, such as your company's or a client's, everywhere a Rhino file keeps text (layers, blocks, object names, user text, notes, views and more) and replaces them before you share the file. You review every place first, and all changes are one undo step.
- **nbRandomColour** randomises layer colours.

**Modelling**

- **nbHandrail** builds configurable handrails with rails, posts, infill and panels.
- **nbPavementPlacer** arrays a paving module along curves.
- **nbTextureShuffle** breaks up repeating textures by shuffling their box mapping.

**Installing**

Numbat is in beta and requires Rhino 8.21 or later, on Windows.

1. In Rhino, open the Package Manager.
2. Tick Include pre-releases.
3. Search for Numbat and click Install.

The stair and PV tools are being reworked and will return in a later release. Guides for every tool, the changelog and the source code are in the [Numbat GitHub repository](https://github.com/tomdufficy/Numbat).

### Project icon

`assets/brand/numbat-icon-512.png` as of commit 8f31a11 (the simplified five-colour logo on a cream rounded square).

### Images

- `assets/demos/nbHandrail.gif`
- `assets/demos/nbPavementPlacer.gif`
- `docs/images/nbAF-where.png`
- `docs/images/nbAF-depth.png`

These are placeholders until nicer demo images are made for the README; use those here too when they exist.

### Other fields

- Category: Architecture
- Licence: MIT
- Website: https://github.com/tomdufficy/Numbat
- Support email: the maintainer's (not recorded here)
- Platform: Windows only
