# How to use nbAF

nbAF draws the Abstandsflächen of your massing model: the areas in front of outer walls that German building codes require to be kept free. It checks every building against every other one and against the plot lines, and exports the result as a 2D drawing. The rules are the Hamburg building code, HBauO § 6 (2025 text), and every number can be changed.

## 1. Model your buildings

- Model each building as stacked volumes: one closed box per storey (or part of a storey), as for nbGFA. A pitched roof can be its own volume on the top storey.
- Volumes that touch become one building. Volumes in the same Rhino group do too, so you can join parts that don't touch.
- Work in millimetres or metres. Depths and areas are always shown in metres.

## 2. Open the window

- Type nbAF on the command line.
- The window stays on top. If you close it, type nbAF again and it reopens where you left it.
- While the window is open, the Abstandsflächen are drawn in the viewports as a live preview. Closing the window hides the preview. Nothing is added to your model until you click Bake.
- Top right, in the window's header: ? opens this guide (or press F1), and ⋯ opens the menu: this guide, the sources, Reset values, Reset colours, Update now, Delete baked output and the display options.
- Drag the window edges to resize it. If the content doesn't fit, the window scrolls.

## 3. Add buildings

- Select the volumes and click Add beside the BUILDINGS heading. With nothing selected, Add asks you to pick them on the command line.
- Each building gets a row: B1, B2, ... numbered from west to east.
- All buildings are treated the same: each one is checked against every other.
- Click a row to select the building's volumes. Right-click a row for more: select, set, select or clear the plot, or stop tracking the building.
- To stop tracking volumes, select them and click Remove. The volumes themselves are not deleted.
- nbAF remembers which volumes it tracks, their role and their plot on the volumes themselves, so it is saved with the file and survives copy and paste.

## 4. Plots

- Click Set plot on a building's row. If one closed curve is selected, it becomes the plot. Otherwise pick it on the command line.
- A row with a plot shows Plot. If the plot curve is deleted, the row shows Plot missing and the window says so.

## 5. What you see

- Every face (side) of every building gets one Abstandsfläche: a filled area with an outline.
- Walls of one building that face the same way form one area together, also when they step back (set-back storeys). nbAF works out each wall's depth from its own position and height, then joins them into one outline, as the Hamburg FAQ on § 6 asks.
- The part of a set-back wall's area that lies over the building's own lower storeys is inside the building, so it is left out. The outline starts at the building's outer wall.
- The Clashes tile shows how many clashes there are. In the viewports each clash zone is filled in the clash colour with a numbered marker, and the CLASHES list in the window says what each number is. Click a clash in the list to zoom to it. Every pair of buildings is checked for:
- Abstandsflächen overlapping each other. This is allowed if the walls stand at more than 75° to each other, and stepped walls of one building facing the same way count as one. Where three or more overlap, each pair is listed.
- An Abstandsfläche reaching into another building.
- An Abstandsfläche lying on another building's plot.
- An Abstandsfläche that leaves its own plot without reaching another plot, for example onto a road, is not flagged. In principle it may cover a public road, green or water area only up to its middle; nbAF does not check that yet.
- Everything updates by itself shortly after you move or edit a tracked volume or a plot line.
- Plots are optional. Without any plots, nbAF says so in the window and checks only overlaps and areas reaching into buildings.
- The footer shows the result: the number of clashes, or that there are none.

## 6. How the depth is worked out

- H is the wall height from the lowest point of the building to the top of the wall, or to where the wall meets the roof.
- A roof flatter than 70° adds one third of its height to H. A steeper roof adds all of it.
- A gable wall counts up to the ridge, so its Abstandsfläche follows the shape of the gable.
- Each part of a façade takes the height of the tallest volume above it. An upper storey that is set back, or covers only part of the floor below, gets its own depth.
- Depth = 0.4 H, at least 2.50 m. In Gewerbe- and Industriegebiete (zone GE/GI) it is 0.2 H, at least 2.50 m.
- Only flat walls get Abstandsflächen. Curved walls are skipped, and the window says how many.

## 7. Change the values

- The RULE VALUES section shows every number nbAF uses, with its paragraph in HBauO § 6. Click a value to change it, for example to 0.8 H when a Bebauungsplan sets that. Fractions such as 1/3 are accepted.
- Click Zone to switch between the general rule and GE/GI.
- A changed value is highlighted and shows its default underneath. The window says that the values differ from the HBauO defaults, and Reset brings them back.
- The values are saved in the Rhino file.

## 8. Display

- Click a colour in the DISPLAY section to change it. The colours apply to the preview, to new baked layers and to exports, and are remembered for your next files.
- Layers that were baked before keep their colour. Change them in the Layers panel.
- Depth labels is off by default, so only clashes are marked. Turn it on to show a depth dimension for each wall: a line with ticks from the middle of the wall straight out to the edge of its Abstandsfläche, with the depth written beside it.
- The dimensions are drawn on the ground, like dimensions on a plan, and read from the bottom or the right. They grow and shrink as you zoom, and text too small to read is hidden until you zoom in. A gable wall's dimension gives the depth at the middle of the wall.
- A wall gets no dimension where its whole area lies over the building itself, or inside the area of a higher wall of the same face. Where two dimensions would overlap, the deeper wall keeps its dimension.

## 9. Bake

- Click Bake to add the result to your model as closed curves, on sublayers of an "nbAF" layer: Areas, Clashes, Plots, Footprints and Labels.
- Each building face is one closed curve, named for example "B3 south, depth 5.12 m". Each clash zone is a closed curve with a numbered dot; hover over the dot to see what the clash is.
- With Depth labels on, the depth dimensions are baked too, as Rhino dimensions on the Labels layer in your current dimension style, with the text set to the depth in metres. Each dimension's name (in Properties) says how H was worked out for that wall.
- Baking again replaces what was baked before. Each bake is one undo step.
- ⋯ > Delete baked output removes everything on the nbAF layers. The buildings stay tracked.

## 10. Export

- Click Export and choose DXF, SVG or JPG. The drawing is a 2D top view in metres with a legend, the values and the source. It always uses the same drawing style, whatever the colours in the window:
- Abstandsflächen: one closed light blue outline per building face, with a pale blue fill in SVG and JPG.
- Clashes: separate closed red outlines, with a pale red fill in SVG and JPG.
- Plot lines: blue dashed lines. Building footprints: black outlines filled white.
- In the Export menu, choose what else goes in: Clash zones, Clash numbers (red numbered markers, also listed under the legend), Clash notes (what each clash is and its area, in two short lines) and Depth dimensions. nbAF remembers your choice.
- DXF has one layer per element. SVG is at 1:500.

## 11. Display options

- ⋯ > Interface size makes the window and its text larger (100 %, 115 % or 130 %).
- ⋯ > Stronger outlines draws darker borders on buttons, rows and dividers.
- These options are shared with the other Numbat windows. The window follows Rhino's light or dark mode.

## Not covered yet

- The half-road rule: an Abstandsfläche may cover a public road, green or water area only up to its middle. nbAF doesn't flag Abstandsflächen that leave their plot onto a road, and doesn't check this limit yet. Baulasten are not covered either.
- Walls built on the plot boundary, balconies and other projections that may be ignored, dormers, and sloping ground.
- Averaging irregular Abstandsflächen (Flächenausgleich). nbAF stays on the safe side.
- Other Länder. Most follow the Musterbauordnung (0.4 H, at least 3 m), so for many of them changing the minimum depth is enough. Check the current law first.

## Commands

- nbAF: opens the window, or brings it to the front.
- nbAFAdd: picks volumes to add on the command line.
- nbAFSetPlot: sets a building's plot, for example nbAFSetPlot B2.
- nbAFBake: bakes the current result to the nbAF layers.
- nbAFClear: deletes everything nbAF baked.
- nbAFSources: lists the legal texts and opens the saved copy or the live page.
- nbAFDemo: adds an example district of about 450 storey volumes (26 buildings) on an "nbAF demo" layer, with a ground surface on "nbAF demo::Ground" that nbAF ignores, so you can try nbAF straight away. It has no plots. Choose Scene=Blocks on the command line for four small test buildings that each have a plot. Running it again replaces them.
- nbAFHelp: shows this guide. You can also open it with the ? button in the window, or press F1 there.

All but nbAF are hidden: they don't appear as you type, but work when typed in full.
