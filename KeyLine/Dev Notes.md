
# Known Bugs

- [ ] when there are nested blocks, dragging distances mess up.

# Feedbacks

- [ ] 

# To Do

- [ ] show a ! next to macro tabs if there is a new version available, tooltip to inform about this.
- [ ] look into preventing input from reaching target window with a remapper option.



### notes
dotnet publish .\KeyLine\KeyLine.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true -o .\publish
