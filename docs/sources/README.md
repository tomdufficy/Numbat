# Sources for nbAF

The legal texts nbAF's rules are based on, saved in case the online versions move or change. The build copies the PDFs next to the plug-in, and nbAF (⋯ > Sources, the guide, or `nbAFSources`) opens either the saved copy or the live page. Keep this list in sync with `src/Numbat/Numbat/Commands/Analysis/NumbatAf/AfSources.cs`.

| File | What it is | Saved | Online |
|------|------------|-------|--------|
| [HBauO-2025-Lesefassung.pdf](HBauO-2025-Lesefassung.pdf) | Hamburgische Bauordnung vom 6. Januar 2025, nichtamtliche Lesefassung, Stand 25.09.2025. § 6 is the basis of nbAF's rules. | 2026-11-23 | [hamburg.de](https://www.hamburg.de/resource/blob/1120174/a1f3778eb712e7a95605fbc6872df915/hamburgische-bauordnung-hbauo-2025-nichtamtliche-lesefassung-data.pdf) |
| [HBauO-FAQ-6-Abstandsflaechen.pdf](HBauO-FAQ-6-Abstandsflaechen.pdf) | Hamburg FAQ on § 6 HBauO (written for an earlier version of the law). | 2026-11-23 | [hamburg.de](https://www.hamburg.de/resource/blob/190284/553ed9c05d0bcf090c18643e534441e0/faq-06-hbauo-abstandsflaechen-data.pdf) |

Linked only (web pages, not saved): the LGV Hamburg pages for the [3D-Stadtmodell](https://www.hamburg.de/politik-und-verwaltung/behoerden/behoerde-fuer-stadtentwicklung-und-wohnen/aemter-und-landesbetrieb/landesbetrieb-geoinformation-und-vermessung/produkte-und-dienstleistungen/geodaten-des-lgv/3d-stadtmodell-244116) and [ALKIS](https://www.hamburg.de/politik-und-verwaltung/behoerden/behoerde-fuer-stadtentwicklung-und-wohnen/aemter-und-landesbetrieb/landesbetrieb-geoinformation-und-vermessung/produkte-und-dienstleistungen/geodaten-des-lgv/alkis-244248), the data sources for neighbouring buildings and plots (not imported yet).

## Rules in nbAF (HBauO § 6, 2025 text)

| HBauO § 6 | Rule | In nbAF |
|-----------|------|---------|
| Abs. 4 | Depth measured perpendicular to the wall. Wall height = ground to where the wall meets the roof skin, or to the top of the wall. | Done. Ground = lowest point of the building. |
| Abs. 4 | Roofs under 70°: add 1/3 of the roof height. 70° or steeper: add the full roof height. Same for Dachaufbauten. | Done for roofs, including a roof modelled as its own volume on the top storey. Dormers are not yet handled separately. |
| Abs. 4 + FAQ | Gable walls ("Giebeldreieck"): the wall height runs to the top of the wall, i.e. the ridge. The Abstandsfläche follows the gable's outline. | Done: depth 0.4 × the height at each point, at least 2.50 m. |
| Abs. 2 + FAQ | Irregular Abstandsflächen may be averaged (Flächenausgleich), only for residential buildings in Gebäudeklassen 1–3. | Not applied (stays on the safe side). |
| FAQ (Abs. 2) | Stepped outer walls (Staffelgeschosse) are not looked at separately: together they form one Abstandsfläche. | Done: each step gets its own depth (H from the ground, measured from the step's own position, Abs. 4), then all walls of a building facing the same way are joined into one Abstandsfläche. The part lying over the building's own lower storeys is inside the building and left out. |
| Abs. 5 | Depth 0.4 H, at least 2.50 m. | Done (default). |
| Abs. 5 | Gewerbe- and Industriegebiete: 0.2 H, at least 2.50 m. | Zone GE/GI in the window. |
| Abs. 5 | A Bebauungsplan can set other depths. | Change the depth factor and minimum depth in the window. |
| Abs. 3 | Abstandsflächen must not overlap, unless the walls are at more than 75° to each other. | Flagged "Overlap" for every pair of buildings, and between non-parallel faces of one building. |
| Abs. 1 | Kept free of buildings. | Flagged wherever an Abstandsfläche reaches into another building. |
| Abs. 2 | Must lie on the plot. | Each building can have its own plot. Flagged only where an Abstandsfläche reaches another building's plot ("on plot of"), for every building. Leaving the own plot towards a road or open land is not flagged. Plots are optional; without them this check is skipped. |
| Abs. 2 | May lie on public roads, green and water areas up to their middle, or on other plots with a Baulast. | Done for roads (and green or water areas) where you add their centre lines: the part past a centre line is flagged, and one ending within 0.5 m before it is listed to check. Baulasten: not yet. |
| Abs. 1 | No Abstandsfläche for walls built on the boundary where that is allowed. | Not yet. |
| Abs. 6 | Projections ignored: cornices and roof overhangs up to 0.50 m; balconies and bays up to 1.50 m deep and 1/3 of the wall width; added insulation up to 0.25 m. | Not yet: model the main volume without them. |
| Abs. 8 | Allowed inside Abstandsflächen: garages and sheds up to 3 m mean wall height and 9 m per boundary, etc. | Not yet. |

Every number above can be changed in the nbAF window and is saved in the Rhino file. The window and exports say when values differ from the HBauO defaults.

When a source is updated: download it again, update the date here and in `AfSources.cs`, and check that the defaults in `AfRules.cs` and the table above still match.
