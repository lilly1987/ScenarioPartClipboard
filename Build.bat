@echo off
setlocal

set "RIMWORLD_DIR=%~dp0..\.."
for %%I in ("%RIMWORLD_DIR%") do set "RIMWORLD_DIR=%%~fI"

set "HARMONY_DLL="
if exist "%RIMWORLD_DIR%\Mods\Harmony\Current\Assemblies\0Harmony.dll" (
    set "HARMONY_DLL=%RIMWORLD_DIR%\Mods\Harmony\Current\Assemblies\0Harmony.dll"
)
if not defined HARMONY_DLL if exist "%RIMWORLD_DIR%\Mods\Harmony\Assemblies\0Harmony.dll" (
    set "HARMONY_DLL=%RIMWORLD_DIR%\Mods\Harmony\Assemblies\0Harmony.dll"
)
if not defined HARMONY_DLL if exist "%ProgramFiles(x86)%\Steam\steamapps\workshop\content\294100\2009463077\1.7\Assemblies\0Harmony.dll" (
    set "HARMONY_DLL=%ProgramFiles(x86)%\Steam\steamapps\workshop\content\294100\2009463077\1.7\Assemblies\0Harmony.dll"
)
if not defined HARMONY_DLL if exist "%RIMWORLD_DIR%\..\..\workshop\content\294100\2009463077\1.7\Assemblies\0Harmony.dll" (
    set "HARMONY_DLL=%RIMWORLD_DIR%\..\..\workshop\content\294100\2009463077\1.7\Assemblies\0Harmony.dll"
)

if not defined HARMONY_DLL (
    echo ERROR: Could not find Harmony's 0Harmony.dll.
    echo Install the Harmony mod or set HarmonyPath in the build command.
    exit /b 1
)

dotnet build "%~dp0Source\ScenarioPartClipboard.csproj" --configuration Release /p:RimWorldDir="%RIMWORLD_DIR%" /p:HarmonyPath="%HARMONY_DLL%"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"

if "%BUILD_EXIT_CODE%"=="0" (
    echo Build succeeded: "%~dp0Assemblies\ScenarioPartClipboard.dll"
) else (
    echo Build failed with exit code %BUILD_EXIT_CODE%.
)

exit /b %BUILD_EXIT_CODE%
