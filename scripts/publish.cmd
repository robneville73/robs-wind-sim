@echo off
cd /d "%~dp0\.."
dotnet publish src\WindSim\WindSim.csproj -p:PublishProfile=win-x64
