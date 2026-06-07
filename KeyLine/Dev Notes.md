
# Known Bugs

- [] 
# Feedbacks

- [ ] 

# To Do

- Make "Target Window Focused" to "Window Focused" with reference.
- redesign the node inspectors
- toggle keys like capslock's node is disgusting 
- add a counter for unlimited loops timelines to show how many times they played,  
   this can count in the status text next to Running and timer

1.9 
1. Toggle Loop Mode, adds 2 fixed position timelines, macro start as first and macro end as last.
this one can cause weird behavior if macro end has delay, so i gotta keep it consistent
for the users. if macro end has delay, macro should run until macro end timeline is over, 
cannot be restarted or started a 2nd time. 
i currently don't support 1 macro running 2 instances. maybe later i can look into that. 

2. Sequence mode, each run of the macro, will play only 1 timeline and move to the next
with each press. play them in order with only 1 shortcut. 
this should have a reset shortcut, which can be set up next to the loop modes in macro options.
should also show which timeline is played last and what is the next timeline to play.
each timeline should have a Cooldown value that prevents them from playing again until the cooldown is over.
if a timeline is on cooldown, skip it and play the next. 
if all timelines are on cooldown, skip all, and wait for the cooldown to finish.

3. Random Mode, play timelines at random. with each run. can be automated by a 2nd macro using "Run Macro"
should also show which timeline is played last and what is the next timeline to play.

---

maybe later 4. Conditional Sequence
make timeline run conditions such as
If timeline 1 is played, next play 2 and skip 3, if not, skip 2 and play 3. needs more details on how it can work.


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




### notes
dotnet publish .\KeyLine\KeyLine.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true -o .\publish
