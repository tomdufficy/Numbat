# How to use nbAnon

nbAnon finds a word or phrase, such as your company's or a client's name, everywhere a Rhino file keeps text, and replaces it before you share the file. You see every place it was found first, and choose which ones to change.

## 1. Find

- Type nbAnon on the command line to open the window.
- The window has four sections: Find, Look in, Found and Replace with. Click a heading (or the arrow before it) to close or open that section; nbAnon remembers which ones you closed. A closed section still shows its count, for example how many places were found, and Tick all and Tick none still work while Found is closed.
- Top right, in the window's header: ? opens this guide, ⋯ opens the menu (this guide and Save report…).
- Type the phrase to look for, for example ACME. Upper and lower case are treated the same unless you turn on Match case.
- Whole words only finds ACME but not ACMEBAU. Leave it off to catch the name inside longer words too.
- Choose where to look (all places are on by default), then click Scan or press Enter.

## 2. Where nbAnon looks

- Layers: every layer name, at every level, and layer user text.
- Blocks: block names and descriptions, and everything inside every block definition. Blocks nested inside other blocks are covered too, at any depth.
- Objects: object names, hyperlinks, attribute user text and geometry user text, also on hidden and locked objects and on layouts.
- Text: text, text dots, leaders and dimension text you typed over.
- Groups: group names.
- Document: document user text (Document Properties > User Text) and the document notes.
- Views: named views, named CPlanes, named positions, layer states, snapshots and layout names.
- Styles: linetype, hatch pattern and dimension style names.
- Materials: material, environment and texture names.
- File paths: the file's own path, linked block files and embedded bitmaps. These are reported only: rename or re-link the files yourself.

## 3. Review

- Every place the phrase was found gets a row: where it is, the text now, and the text after.
- Untick a row to leave it as it is. Tick all and Tick none change every row at once.
- Rows marked report only can't be changed from nbAnon. Snapshot names are among them: rename those in the Snapshots panel.
- Save report… writes the list to a text file, for your records.

## 4. Replace and anonymise

- Random code: a code such as QX7KMR.
- Random word: a neutral word such as Heron. Click New for another one.
- My text: type the replacement yourself, for example Client. The text box appears only in this mode.
- Remove: the phrase is taken out. A name that would end up empty gets a random code instead.
- The same replacement is used everywhere, so the layer, the block and the note all say the same thing afterwards.
- Click Anonymise. All changes together are one step: Ctrl+Z puts them back, except group names, named CPlanes and the document notes, which Rhino can't undo. Save the report first if you may need the old names.
- nbAnon scans again straight away, so the list shows anything that is left.
- If a new name is already taken, nbAnon adds a number (Heron 2).

## 5. What nbAnon can't change

- Rhino saves who created and last saved a file, and a preview image of the viewport. nbAnon can't reach these. To be safe, start a new file, Import or Insert the anonymised model, and save under a neutral name.
- Data that other plug-ins store in the file (for example Grasshopper definitions, VisualARQ or render engines) isn't searched.
- Named CPlanes have no rename in Rhino, so nbAnon replaces each one with a copy under the new name.

## Commands

- nbAnon: opens the window.
- nbAnonHelp: shows this guide. You can also open it with the ? button in the window.
