
# Known Bugs

- [ ] open restarting nodes will start from center/center-ish position.

# Feedbacks

- [ ] 

# To Do

1.9
- add Disable timeline that will allow removing the timeline from macro without deleting it. so they can be activated later.
- better timeline header
- fix reset button
- fix loops delay on non-loop timelines
- fix name change focus

2.0
- add controller support
- update mouse recording and playback for foreground mouse actions.
  - RECORDING: currently mouse recording only has click recordings. 
  - add a support for recording mouse movement and clicks in the target window, and build the actions in timeline. 
  - Mouse drag doesn't needs to be recorded, just clicks and their position as long as mouse recording is active.
  - timeline should be build with existing nodes, for example, if click occured, 3 nodes will appear, Delay > move mouse > Mouse click ( whatever the button is )
  - PLAYBACK: During playback, each mouse click and movement should show a visual. 
  - maybe a trail like line between where mouse was and where it moved, 
  - and a circle around mouse scaling up/down with presses and a text of what click was made.
- add a Feedback/bug report options in About page.




3.0???
![img.png](img.png)

After KeyLine 2.0 mouse/gamepad updates, start a major Logic Editor update.
This update should be a large UI-focused push, not a small inspector feature.
Direction: Warcraft 3 Map Editor-style visual scripting, not full programming or Blueprint complexity.
Users create simple macro variables/state from the UI, then select them through dropdowns.
Initial state types should be limited to Counters, Flags, and Timers.
Conditions should operate only on KeyLine’s own internal state, input state, macro state, and window state.
Add visual logic/control nodes such as If, Repeat, Set Counter, Toggle Flag, Reset Timer, Run Macro, Stop Macro.
Keep the system structured and readable, avoiding freeform expressions or script boxes.
Redesign the editor UI around visual flow so logic, conditions, and state changes are easy to understand.


### notes
dotnet publish .\KeyLine\KeyLine.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true -o .\publish
