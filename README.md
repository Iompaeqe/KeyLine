# MacroSpammer

License: MIT

A lightweight window-targeted macro recorder/spammer for Windows.

My goal was simple:

Select a target window → record/edit a macro → let it run in the background while continuing to use the PC normally.

No giant scripting system.  
No bloated automation suite.  
Just a fast and practical macro tool.

MacroSpammer is focused on simple window-targeted keyboard and mouse macros, with a visual timeline editor that makes macros easy to record, edit, and run.

---

## Screenshot

<img width="1437" height="1094" alt="image" src="https://github.com/user-attachments/assets/eeb7cc26-cf25-4446-ad2a-7c6b8c07d910" />

---

## How it works

MacroSpammer can send keyboard input directly to selected windows using WinAPI window messages (`PostMessage` / `SendMessage`) instead of only relying on global keyboard simulation.

This allows compatible macros to run on unfocused/background windows while you continue using your PC normally.

Some applications handle this perfectly, while others may partially or completely ignore it depending on how they process input internally.

Mouse input is also supported. Foreground mouse input works normally, while background mouse input is experimental and only works with some applications.

---

## Features

- Window-targeted keyboard macro playback
- Keyboard and mouse recording
- Mouse button support for `M1` through `M5`
- Foreground mouse input support
- Experimental background mouse input support
- Cursor move nodes
- Mouse coordinate target picker
- Multiple macro tabs
- Multiple timelines per macro
- Visual timeline editing
- Drag & reorder timeline nodes
- Standard delay mode
- Loop and timer support
- Global macro shortcuts
- Multiple macros can run at the same time
- Per-timeline input mode: `Key` or `Text`
- Target window selector
- Auto-save latest state
- Compact UI with tooltips

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

After recording, nodes can be edited, reordered, removed, or adjusted directly inside the timeline.

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

## Global Shortcuts

Each macro can have a global shortcut.

Shortcuts can start macros while MacroSpammer is not focused, and different macros can run at the same time through their shortcuts.

This also allows MacroSpammer to be used as a simple key remapper.

---

## Notes / Limitations

MacroSpammer uses different input methods depending on the selected node/input type.

Because Windows applications do not all process input the same way, compatibility can vary:

- Background keyboard input works well in many normal desktop applications.
- Some applications may ignore windows-message-based keyboard input.
- Foreground mouse input works normally.
- Experimental background mouse input works in some applications.
- Many games and protected applications may ignore background mouse input.

This is a practical macro tool, not a guaranteed universal automation system.

---

## Possible Future Features

- Import/export macro profiles
- Macro, timeline, and node duplication
- Better macro tab status indicators
- Better multi-window workflow
- Conditional execution
- Repeat / for-loop style timeline nodes
- Hold/toggle modes
- More advanced timeline visuals
