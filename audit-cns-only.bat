@echo off

set OVERRIDE_MODS_FOLDER=~mods\CustomNanosuitSystem
set EXE_PATH=DekPakModAudit.exe

REM Run the app and override mods folder configuration
%EXE_PATH% --modsfolder="%OVERRIDE_MODS_FOLDER%"

pause
