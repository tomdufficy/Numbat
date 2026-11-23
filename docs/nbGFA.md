# How to use nbGFA

nbGFA tracks the gross floor area (GFA) of your massing as you design. You give each volume a use, such as Residential or Office, and the nbGFA window shows the GFA of each use, its share of the total, and whether it breaks any limits you set.

## 1. Model your volumes

- Model each storey as its own closed box (a closed polysurface or extrusion).
- Stack boxes on top of each other to make buildings.
- The GFA of a box is the area of its flat bottom face, so one box equals one floor plate.
- Work in millimetres or metres. GFA is always shown in m².
- Imperial units (feet, inches) are not officially supported. nbGFA warns you and shows ft² as a best effort.

## 2. Open the window

- Type nbGFA on the command line.
- The window stays on top. If you close it, type nbGFA again and it reopens where you left it.
- Top right, in the window's header: the chart button switches between bars and a pie chart, ? opens this guide, and ⋯ opens the menu. The menu has this guide (How to use nbGFA), the tracking mode, the chart view, Check volumes, Add use, and the display options (see 10).
- The Add use button beside the USES heading adds a new use.
- Drag the window edges to resize it. nbGFA remembers its size and position, and switching between bars and pie never changes the size. If the content doesn't fit, the window scrolls.

## 3. Choose how uses are tracked

Click ⋯ and pick one. Each document remembers its choice. You can switch at any time and your assignments carry across.

- By object (default): the use is stored on each volume and shown by its colour. Your own layers are left alone.
- By layer: nbGFA makes an "nbGFA" layer with one sublayer per use (nbGFA::Residential, nbGFA::Office, nbGFA::Unassigned, ...). The layer a volume sits on is its use. Volumes on any other layer are not counted.

In By layer mode you can work either way, and both count the same:

- Click a use in the window to move the selected volumes onto its layer.
- Or move volumes onto a use layer yourself, for example with the Layers panel or ChangeLayer.
- Layers nested under a use layer count as that use too (for example nbGFA::Residential::Block A).
- A new sublayer you make under nbGFA becomes a new use. Renaming or recolouring a use layer renames or recolours the use.
- When nbGFA moves a volume onto a use layer, it remembers the layer the volume came from. Excluding the volume puts it back there, out of the total. If that layer has since been deleted, the volume goes onto the nbGFA layer itself, which is not counted.
- Switching back to By object puts volumes back on the layers they came from and stores their use on each volume. Volumes you moved onto a use layer yourself stay where they are.

## 4. Assign uses

- Select one or more volumes, then click a use in the window (for example Residential).
- The volumes take on the use's colour, and the window updates straight away.
- Assigning a volume that already has a use moves it to the new use.
- You can also use the nbGFAAssign command: select volumes, then pick a use on the command line.
- Assignments are saved in the .3dm file and kept when you copy, move or mirror.
- Not sure of the use yet? Assign the volume to Unassigned. It counts toward TOTAL GFA like any other use, so you always see the full area, and you can move it to its real use later. Unassigned is an ordinary use: you can rename, recolour, limit or remove it.
- In short: Unassigned means counted, use not decided yet. Excluded means not counted at all.

## 5. Read the numbers

- TOTAL GFA is the sum of all volumes that have a use, including Unassigned. Boxes you have never given a use, and excluded boxes, are not counted.
- The Total GFA box also shows the tracking mode: by object or by layer.
- The Selected box shows the GFA of whatever volumes you have selected right now, assigned or not.
- Each use card shows its colour, GFA, share of the total, and how many volumes it has. The bar along the bottom of the card fills up with its share.
- The line at the bottom of the window tells you whether all uses, and the total, are within their limits.

## 5a. Bars or pie chart

- Click the small pie chart button in the window's header to show the uses as a pie chart. Click it again (it now shows bars) to go back. nbGFA remembers your choice.
- Each slice has a label beside the pie with the use's name, share and GFA.
- The pie works just like the bars: click a slice or its label to assign the selected volumes, and right-click either one for the options.
- Uses that have no GFA yet appear as small chips under the pie, so you can still click them to assign volumes.
- A use that breaks its limit gets a red outline on its slice, and its label says what is wrong, for example "over max 30%".

## 6. Set limits

- Right-click a use, then choose Set max % or Set min % to limit its share of the total. For example, set Residential to a max of 30%.
- Or choose Set max m² or Set min m² to limit its area. For example, set Retail to a min of 500 m².
- You can combine both kinds. The limits show at the bottom right of the use's card.
- A use that breaks a limit gets a red outline and a warning sign with words such as "over max 30%" or "under min 500 m²".
- To remove one limit, open it again and leave the box empty. Right-click, then choose Clear limits to remove them all.
- Limits are saved with the document.

## 6a. Total GFA target

- Right-click the Total GFA box (or use ⋯ → Total GFA target), then choose Set max total m² or Set min total m².
- When the total is outside the target, a small warning sign appears in the corner of the Total GFA box. Hover over the box to see how far over or under you are, for example "1,200 m² over the max of 10,000 m²".
- When the total is on target, nothing is shown. Hover over the box to see the target.
- Choose Clear target to remove it. The target is saved with the document.

## 7. Manage uses

- Click Add use, or right-click a use and choose Add use, to add a new use with a name and colour.
- Right-click a use, then choose Rename / colour to change it. Its volumes (and its layer) follow automatically.
- Right-click a use, then choose Remove to delete it. Its volumes move to Unassigned, so they stay in the total. If you have removed Unassigned too, they are excluded instead.
- Each document keeps its own list of uses.

## 8. Right-click options on a use

- Assign selected: same as clicking the card.
- Exclude selected from GFA: takes the selected volumes out of the total. By object, their use is removed. By layer, they go back to the layer they came from.
- Select all: selects every volume with that use.
- Exclude all from GFA: takes all of that use's volumes out of the total.

## 9. Fix problem volumes

nbGFA checks every assigned volume as you work. When something is wrong, a box with a warning sign and "volume issue(s)" appears in the window. Click Review in that box, or run nbGFACheck, to see what's wrong and fix it.

- Bottom slightly tilted: the bottom face is up to 5° off level. It is still counted. Flatten rotates the volume level about the centre of its bottom face. Ignore accepts it as it is and stops flagging it.
- Not closed: an open polysurface has no volume and is not counted. Cap or join it.
- No flat bottom: no level bottom face could be found, so it is not counted. Model each storey as a box with a flat floor.
- Not a polysurface: meshes, surfaces and curves can't be measured and are not counted.
- Overlapping volumes: two volumes share floor area at the same height, so that area is counted twice. The check looks for these when you open it. Trim or move the volumes so they only touch.

Use Select in the check window to select the problem volumes in Rhino.

## Commands

nbGFA is the only one that shows up as you type. The others are helpers for macros and keyboard users: type their full name and press Enter.

- nbGFA: opens the window, or brings it to the front.
- nbGFAAssign: assigns a use from the command line.
- nbGFACheck: checks assigned volumes for problems.
- nbGFADemo: adds three example blocks of 2, 4 and 6 storeys on an "nbGFA demo" layer, so you can try nbGFA straight away. Running it again replaces them.
- nbGFAHelp: shows this guide. You can also open it with the ? button in the window, or press F1 there.

## 10. Look, size and keyboard

- The window follows Rhino: light when Rhino is light, dark when Rhino is in dark mode.
- In the ⋯ menu, Interface size makes the window and its text 100 %, 115 % or 130 % of normal. Stronger outlines makes borders around cards, buttons and fields darker. nbGFA remembers both.
- You can use the window without a mouse. Click in it (or press Tab until it is active), then use Tab or the arrow keys to move between buttons and uses. Enter or Space clicks. Shift+F10 opens a use's right-click options. F1 opens this guide.
- In nbGFA's small windows (Add use, Set max %, Check volumes), Enter is OK and Esc cancels.

## Tips

- Undo works for assigning, excluding, flattening and switching modes.
- Hidden and locked volumes still count towards GFA.
