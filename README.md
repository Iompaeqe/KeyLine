# KeyLine

**KeyLine** is a lightweight timeline-based macro recorder and editor for Windows.

Create macros visually, arrange actions on timelines, assign shortcuts, and run them against a selected target window without writing scripts.

<p align="center">
  <img width="900" alt="KeyLine" src="https://github.com/user-attachments/assets/04ab499a-f58d-41f4-a8ff-88f3ba18700b" />
</p>

---

## Download

Download the latest version from the [Releases](https://github.com/Iompaeqe/KeyLine/releases) page.

KeyLine is distributed as a portable Windows executable.  
No installation is required.

---

## Why KeyLine?

Most macro tools feel either too old, too complicated, or too focused on recording everything.

KeyLine focuses on making macro creation fast, visual, and easy to understand.

One of KeyLine’s main goals is **background keyboard input**: it can send keyboard actions to a selected target window without taking focus from what you are currently doing.

This means a macro can run against another application while you continue using your PC normally.

> Background input currently applies to keyboard actions.
> Mouse background input is experimental and depends heavily on how the target application handles input.

KeyLine focuses on:

* Fast macro creation
* Visual timeline editing
* Background keyboard input to a selected target window
* Shortcuts and remapping
* Multiple timelines
* Window-aware automation
* Reusable macro workflows
* Keeping the UI clean and understandable

---

## Macro Creation

Build macros by adding actions such as key presses, text, delays, mouse input, and blocks directly onto the timeline.

<p align="center">
  <img width="760" alt="Macro Creation" src="https://github.com/user-attachments/assets/734b7bb9-aff2-4681-a607-5eb183388ea5" />
</p>

KeyLine uses timelines instead of long script-like action lists. Nodes can be moved, edited, copied, duplicated, deleted, and arranged visually.

---

## Macro Shortcuts

Assign a shortcut to a macro and trigger it without pressing the Start button manually.

<p align="center">
  <img width="760" alt="Macro Shortcut" src="https://github.com/user-attachments/assets/4856742b-45f1-4750-8ef7-0138015725dd" />
</p>

Shortcuts are useful for repeated actions, quick text input, game/app automation, toggle-style macros, and remap behavior.

---

## Remap

Remap lets a shortcut key trigger a macro without sending the original key to the target window.

For example:

```text
Shortcut: 1
Macro: Q > Q > Q
Result: Q > Q > Q
Not:    1 > Q > Q > Q
```

Remap only works when the selected target window is focused, so it does not consume inputs while you are using other apps.

Global Remap can be turned off temporarily when you want to type normally, such as in an in-game chat.

<p align="center">
  <img width="760" alt="Remap" src="https://github.com/user-attachments/assets/1c2f7873-b89f-4606-bcd9-ef371eb36380" />
</p>

---

## Timelines and Loop Modes

A macro can contain multiple timelines.

This makes it easier to separate actions instead of forcing everything into one long sequence.

Loop modes control how timelines run:

* **Async** — timelines run independently.
* **Sync** — timelines wait and restart together.
* **Cycle** — one loop of each available timeline runs in order.
* **Chain** — each timeline fully completes before the next starts.

<p align="center">
  <img width="800" alt="Timelines and Loop Modes" src="https://github.com/user-attachments/assets/900fde08-d138-43c3-ab7f-cff72784c352" />
</p>

---

## Repeat Blocks

Repeat blocks let you loop a group of nodes without duplicating them manually.

<p align="center">
  <img width="760" alt="Repeat Block" src="https://github.com/user-attachments/assets/0805d683-e79b-43c5-b751-a03d4c8b309f" />
</p>

---

## Condition Blocks

Condition blocks let part of a macro run only when the given condition is true.

Current condition types:

* Held key
* Random chance
* Every N loop / repeat
* Pixel color
* Target Window Focused
* Window Exists
* Macro Running
* Time Passed

<p align="center">
  <img width="760" alt="Condition Blocks" src="https://github.com/user-attachments/assets/3a9d5a1c-84c6-4084-bef3-c92aed9bbf2c" />
</p>

---

## Run Macro and Reusable Workflows

Macros can trigger other macros using the Run Macro node.

This allows larger workflows to be split into smaller reusable pieces instead of duplicating the same timeline logic multiple times.

Only macros from the active profile can be selected and executed.

---

## Recording

KeyLine supports keyboard recording.

Recorded input is converted into editable timeline nodes, so you can record first and then clean up the macro manually.

---

## Profiles and Import / Export

Macros can be organized into profiles.

Profiles make it easier to separate different macro groups, such as different games, apps, or workflows.

KeyLine also supports import/export for sharing macros, moving profiles, or backing up all app data.

---

## Notes and Limitations

KeyLine sends input to a selected target window, but not every application handles simulated or background input the same way.

Some games or programs may block, ignore, or handle simulated input differently.

Background mouse input is experimental and may work in some applications but not others.

Use KeyLine responsibly and follow the rules of any software or game you use it with.

---

## Current Status

KeyLine is in active development.

The goal is to keep it simple to use while gradually adding more powerful macro-building tools.

--

## Feedback and Bug Reports

KeyLine is developed in my spare time and is still evolving.

If you encounter bugs, compatibility issues, confusing behavior, or have feature suggestions, please open an issue on GitHub.

When reporting a problem, include:

* KeyLine version
* Windows version
* Target application (if relevant)
* Steps to reproduce the issue

Feedback from real users is the most valuable way to improve KeyLine.

---

## Planned / Future Ideas

Possible future improvements include:

* More examples and tutorials
* More condition options
* More polish for block editing
* Further background input improvements where possible
* Mouse recording and playback improvements
* Controller support
* More advanced macro organization tools

---

## License

## License

KeyLine is licensed under the **GNU General Public License v3.0**.

See the full license here: [GNU General Public License v3.0](https://github.com/Iompaeqe/KeyLine-code/tree/1.8?tab=GPL-3.0-1-ov-file)

