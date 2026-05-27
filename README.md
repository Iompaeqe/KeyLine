# KeyLine

License: MIT

A lightweight window-targeted macro recorder for Windows.

My goal was simple:

Select a target window → record/edit a macro → let it run in the background while continuing to use the PC normally.

No giant scripting system.  
No bloated automation suite.  
Just a fast and practical input automation tool.

KeyLine supports keyboard and mouse macros, multiple timelines, multiple macro tabs, global shortcuts, and a compact visual timeline editor.

---

## Screenshot

<img width="1330" height="601" alt="MacroSpammer screenshot" src="https://github.com/user-attachments/assets/aed82fff-b5e9-4586-8624-1a76b1a98592" />

---

## How it works

MacroSpammer can send keyboard input directly to selected windows using WinAPI window messages (`PostMessage` / `SendMessage`) instead of only relying on global keyboard simulation.

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
- Mouse button support for `M1` through `M5`
- Cursor move nodes
- Text input nodes
- Delay and random delay nodes
- Loop and timer support
- Standard delay mode
- Per-timeline input mode: `Key` or `Text`

### Timeline Editor

- Visual timeline-based macro editing
- Multiple timelines per macro
- Multiple macro tabs
- Drag & reorder nodes
- Edit, copy, paste, duplicate, and delete nodes
- Copy, paste, and duplicate timelines
- Copy, paste, and duplicate macros
- Undo / redo support
- Multi-select support
- Select all support

### Shortcuts

- Global macro shortcuts
- Global start / stop / pause shortcuts
- Emergency stop shortcut
- Configurable editor shortcuts
- Shortcuts can use multi-key combinations
- Multiple macros can run at the same time

### Settings

- Global settings panel
- Launch on Windows startup
- Start minimized
- Minimize to tray
- Close to tray
- Default macro values
- Recording options
- Playback options
- Import / export macros
- Reset settings
- Reset macros
- Experimental nodes can be enabled/disabled from settings

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
- Mouse Click
- Cursor Move
- Experimental Background Mouse Input

Recording captures keyboard inputs, mouse inputs, and the delays between them.

After recording, nodes can be edited, reordered, copied, pasted, duplicated, removed, or adjusted directly inside the timeline.

---

## Input Modes

Each timeline can use either `Key` mode or `Text` mode.

`Key` mode is useful for normal key macros, shortcuts, and key down/up behavior.

`Text` mode is useful for sending background text input to compatible applications. It can avoid problems caused by real keyboard modifier states, such as accidentally turning text into shortcuts like `CTRL+S`.

---

## Standard Delay Mode

When enabled:

- recorded delays are hidden
- a fixed delay value is used between actions

This makes macros easier to read and edit when exact recorded timing is not needed.

---

## Import / Export

Macros can be exported and imported from the settings panel.

Imported macros are added as new macros, making it easier to back up, share, or move macros between installations.

---

## Notes / Limitations

MacroSpammer uses different input methods depending on the selected node/input type.

Because Windows applications do not all process input the same way, compatibility can vary:

- Background keyboard input works well in many normal desktop applications.
- Some applications may ignore window-message-based keyboard input.
- Foreground mouse input works normally.
- Experimental background mouse input works in some applications.
- Many games and protected applications may ignore background mouse input.

This is a practical macro tool, not a guaranteed universal automation system.

---

## Possible Future Features

Things I may add in the future:

- Repeat / for-loop style timeline nodes
- Better macro tab status indicators
- Better multi-window workflow
- Conditional execution
- Hold/toggle modes
- More advanced timeline visuals
