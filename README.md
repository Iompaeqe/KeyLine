# KeyLine

License: MIT

A lightweight window-targeted macro recorder/spammer for Windows.

My goal was simple:

Select a target window → record/edit a macro → let it run in the background while continuing to use the PC normally.

No giant scripting system.
No bloated automation suite.
Just a fast and practical macro recorder.

The goal is to have a lightweight and practical macro tool focused specifically on window-targeted keyboard spam/macros, without turning into a massive automation framework.

---

## Screenshot

<img width="1330" height="601" alt="image" src="https://github.com/user-attachments/assets/aed82fff-b5e9-4586-8624-1a76b1a98592" />

---

## How it works

KeyLine sends keyboard input directly to selected windows using WinAPI window messages (`PostMessage` / `SendMessage`) instead of globally simulating keyboard input.

This allows macros to run on unfocused/background windows while you continue using your PC normally.

Some applications handle this perfectly, while others may partially or completely ignore inputs depending on how they process keyboard messages internally.

---

## Features

- Record keyboard macros
- Send input to unfocused windows
- Multiple timelines
- Multiple macro tabs
- Editable timeline nodes
- Drag & reorder nodes
- Delay nodes
- Text nodes
- Standard delay mode
- Combo visualization (`CTRL+S+A`)
- Loop support
- Auto-save latest state

---

## Timeline system

Macros are built from timeline nodes.

Supported node types:
- Key Down
- Key Up
- Delay
- Text

Recording captures:
- key down events
- key up events
- delays between inputs

---

## Standard delay mode

When enabled:
- recorded delays are hidden
- a fixed delay value is used between actions

This makes macros much easier to read and edit if you have no use for delay.

---

## Planned Features

Things I may add in the future:

- Mouse support
- Background mouse input for specific window positions (if reliable implementation is possible)
- Save/load macro profiles
- Timeline duplication
- Import/export system
- Better timeline visuals/animations
- Per-node settings
- Conditional execution
- Randomized delays
- Hold/toggle modes
- Better multi-window workflow

