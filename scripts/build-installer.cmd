@echo off
setlocal enabledelayedexpansion

cd /d "%~dp0\.."

echo Publishing WindSim...
call "%~dp0publish.cmd"
if errorlevel 1 exit /b 1

for /f "usebackq delims=" %%V in (`powershell -NoProfile -Command "(Select-Xml -Path 'src\WindSim\WindSim.csproj' -XPath '//Version').Node.InnerText"`) do set VERSION=%%V

if not defined VERSION (
    echo Could not read Version from WindSim.csproj
    exit /b 1
)

set ISCC=
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" (
    set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
) else if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" (
    set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
)

if not defined ISCC (
    echo Inno Setup compiler not found. Install with: winget install JRSoftware.InnoSetup
    exit /b 1
)

if not exist artifacts mkdir artifacts

echo Building installer v%VERSION%...
"%ISCC%" /DMyAppVersion=%VERSION% installer\WindSim.iss
if errorlevel 1 exit /b 1

echo.
echo Installer: artifacts\RobsWindSim-Setup-%VERSION%.exe
