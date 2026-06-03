
# KeyLine

License: MIT

KeyLine is a lightweight timeline-based macro recorder and editor for Windows.

It lets you select a target window, record or build macros, organize them into profiles, and run them using visual timelines. In compatible applications, keyboard input can be sent to an unfocused/background window while you continue using your PC normally.

KeyLine is not meant to be a huge automation framework. It is designed to be simple, practical, and fast to use.

---

<img width="2286" height="745" alt="image" src="https://github.com/user-attachments/assets/883ec9d6-ba85-4091-8e74-b4582252cfb7" />

---

## Features

* Visual timeline-based macro editing
* Macro profiles
* Multiple macro tabs per profile
* Multiple timelines per macro
* Keyboard recording
* Mouse recording
* Key down / key up nodes
* Text nodes
* Delay and random delay nodes
* Mouse input nodes
* Cursor move nodes
* Per-macro shortcuts
* Profile-scoped macro shortcuts
* Global playback shortcuts
* Undo / redo
* Copy / paste / duplicate for nodes, timelines, and macros
* Macro/profile import and export
* Full backup export/import for moving KeyLine data between PCs
* Tray support
* Auto target window matching
* Experimental background mouse input

---

## Profiles

Profiles let you organize macros into separate groups.

Each profile has its own macro tabs and macro shortcuts. Switching profiles changes which macros are visible and active.

---

## Inspector Panel

KeyLine uses an Inspector panel for selected timeline and node settings.

The UI is split clearly:

* Macro settings are in OPTIONS.
* Timeline settings are in the Inspector.
* Node settings are in the Inspector.

This keeps the main window cleaner while still allowing more advanced macro setups.

---

## Loop Modes

KeyLine supports four macro loop modes:

* **Async**: Timelines loop independently.
* **Sync**: Timelines wait for each other before starting the next loop.
* **Cycle**: Runs one loop of each timeline in order, skipping finished timelines.

  * Example: A×3, B×5, C×4 → ABCABCABCBCB
* **Chain**: Fully completes each timeline before starting the next.

  * Example: A×3, B×5, C×4 → AAABBBBBCCCC

Loop count and loop delay are set per timeline.

---

## Import / Export

KeyLine supports different export types depending on the use case.

For sharing:

* Export selected macros
* Export selected profiles

For backup or PC migration:

* Export everything

Full export includes profiles, macros, and settings. Full import intentionally replaces the current local KeyLine data, so it is meant for personal backup, formatting your PC, or moving to another PC.

---

## Background Input

KeyLine can send keyboard input to selected windows using WinAPI window messages.

This can allow macros to run on compatible background windows, but support depends on the target application.

Some apps accept background input. Some ignore it. Some games or protected applications may block it completely.

Background mouse input is experimental and only works in some applications.

---

## Limitations

KeyLine does not guarantee that every application will accept background input.

Compatibility depends on how the target window handles input.

Foreground input should behave normally. Background input is application-dependent.

---

## Planned Features

* Repeat nodes
* Conditional nodes
* More advanced timeline controls
* Better background mouse support if a reliable method is found

