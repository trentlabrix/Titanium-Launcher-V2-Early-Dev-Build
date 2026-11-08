@echo off
setlocal
title Titanium Launcher - Build
cd /d "%~dp0"
REM  Usage:
REM    build.bat        single self-contained TitaniumLauncher.exe (no .NET install needed to run it)
REM    build.bat lite   small build, players need the .NET 8 Desktop Runtime installed
where dotnet >nul 2>nul
if errorlevel 1 goto nodotnet
set "PROJECT=TitaniumLauncher\TitaniumLauncher.csproj"
set "OUT=%~dp0dist"
REM Publish updates dist without deleting the existing folder.
echo.
echo  Building Titanium Launcher...
echo.
if /i "%~1"=="lite" goto lite
dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false ^
  -o "%OUT%"
goto check
:lite
dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained false ^
  -p:DebugType=none -p:DebugSymbols=false ^
  -o "%OUT%"
:check
if errorlevel 1 goto failed
echo.
echo  Done. Your launcher is in:  %OUT%\TitaniumLauncher.exe
echo.
explorer "%OUT%"
pause
exit /b 0
:nodotnet
echo.
echo  The .NET SDK was not found.
echo  Install the .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0
echo  then run build.bat again.
echo.
pause
exit /b 1
:failed
echo.
echo  The build failed. Scroll up to read the first error message.
echo.
pause
exit /b 1
