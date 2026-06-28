@echo off
cd /d "%~dp0\.."
dotnet publish src\RobsWindSim\RobsWindSim.csproj -p:PublishProfile=win-x64
