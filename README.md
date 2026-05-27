# KeyLine

License: MIT

A lightweight window-targeted macro recorder for Windows.

My goal was simple:

Select a target window → record/edit a macro → let it run in the background while continuing to use the PC normally.

No giant scripting system.
Just a fast and practical macro recorder.

The goal is to have a lightweight and practical macro tool focused specifically on window-targeted keyboard and mouse macros, without turning into a massive automation framework.

---

## Screenshot

<img width="2080" height="793" alt="image" src="https://github.com/user-attachments/assets/ead59753-008d-4e81-9c7a-d38b15f6598e" />

---

## How it works

KeyLine can send keyboard input directly to selected windows using WinAPI window messages (`PostMessage` / `SendMessage`) instead of only relying on global keyboard simulation.

This allows compatible macros to run on unfocused/background windows while you continue using your PC normally.

Some applications handle this well, while others may partially or completely ignore it depending on how they process input internally.

Mouse input is also supported. Foreground mouse input works normally, while background mouse input is experimental and only works with some applications.

---

## Features

### Macro Recording & Playback

- Record keyboard input
- Record mouse input
- Send keyboard input to unfocused/background windows
- Foreground mouse input support
- Experimental background mouse input support
- Cursor move nodes
- Text input nodes
- Delay and random delay nodes
- Loop and timer support

### Timeline Editor

- Visual timeline-based macro editing
- Multiple timelines per macro
- Multiple macro tabs
- Drag & reorder nodes
- Edit, copy, paste, duplicate, and delete nodes
- Copy, paste, and duplicate timelines
- Copy, paste, and duplicate macros
- Undo / redo support

---

## Timeline System

Macros are built from timeline nodes.

Supported node types include:

- Key Down
- Key Up
- Text
- Delay
- Random Delay
- Mouse Down
- Mouse Up
- Cursor Move
- Experimental Background Mouse Input

Recording captures keyboard inputs, mouse inputs, and the delays between them.

After recording, nodes can be edited, reordered, copied, pasted, duplicated, removed, or adjusted directly inside the timeline.

---

## Import / Export

Macros can be exported and imported from the settings panel.

---

## Notes / Limitations

KeyLine uses different input methods depending on the selected node/input type.

Because Windows applications do not all process input the same way, compatibility can vary:

- Background keyboard input works well in many normal desktop applications.
- Some applications may ignore window-message-based keyboard input.
- Foreground mouse input works normally.
- Experimental background mouse input works in some applications.
- Many games and protected applications may ignore background mouse input.

This is a practical macro tool, not a guaranteed universal automation system.

---

## Planned Features

- Repeat / for-loop style timeline nodes
- Resizable timeline view for better long macro visualization
- Conditional nodes
- New run behavior, Repeat while holding.
