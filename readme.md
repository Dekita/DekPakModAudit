# DekPakModAudit
A simple command line tool to audit the pak files for Stellar Blade.

Logs information on mods that override default assets, and mods that have conflicting chunk id's. 

Use `audit-all-mods.bat` file to audit all mods for the game
Use `audit-cns-only.bat` file to audit mods within the ~mods/CustomNanosuitSystem directory for the game

## Build notes: 
Must have CUE4Parse in the parent directory to build this program. 
eg: `../CUE4Parse/CUE4Parse/CUE4Parse.csproj`
