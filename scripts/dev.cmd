@echo off
cd /d "%~dp0\.."

echo Stopping any running WindSim instances...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0stop-windsim.ps1"

dotnet run --project src\WindSim\WindSim.csproj -c Release
