@echo off
cd /d "%~dp0\.."

echo Stopping any running Robs Wind Sim instances...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0stop-robs-windsim.ps1"

dotnet run --project src\RobsWindSim\RobsWindSim.csproj -c Release
