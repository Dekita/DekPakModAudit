@echo off
setlocal

REM Set project path (optional: update if needed)
set PROJECT=DekPakModAudit.csproj

@REM Clean the project before building
dotnet clean

REM Run the publish command
dotnet publish %PROJECT% ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  /p:PublishSingleFile=true ^
  /p:IncludeNativeLibrariesForSelfExtract=true ^
  /p:DebugType=None ^
  /p:DebugSymbols=false

pause
