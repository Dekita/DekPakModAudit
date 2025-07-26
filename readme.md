# DekPakModAudit
A simple command line tool to audit the pak files for Stellar Blade.

Logs information on mods that override default assets, and mods that have conflicting chunk id's. 

Use `audit-all-mods.bat` file to audit all mods for the game
Use `audit-cns-only.bat` file to audit mods within the ~mods/CustomNanosuitSystem directory for the game


## How to use:
Place the release/built folder in your StellarBlade root folder. 
eg: `S:\SteamLibrary\steamapps\common\StellarBlade\DekPakModAudit`

Run `DekPakModAudit.exe` to run with the default configuration found in `input` folder. 
Run `audit-all-mods.bat` to run with the default config, but a fixed path to ~mods
Run `audit-cns-only.bat` to run with the default config, but a fixed path to ~mods/CustomNanosuitSystem



## Build notes: 
Must have CUE4Parse in the parent directory to build this program. 
eg: `../CUE4Parse/CUE4Parse/CUE4Parse.csproj`
