@echo off
:: FaceOff fallback script: re-enable FACEIT anti-cheat.
:: FACEIT only accepts a driver loaded during startup, so the driver is set to load
:: with Windows and a restart is needed before you can play.

net session >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Run this script as administrator: right-click ^> Run as administrator.
    pause
    exit /b 1
)

echo Setting faceit kernel driver to load with Windows...
sc config faceit start= system

echo Setting FACEITService to manual start...
sc config FACEITService start= demand

echo.
echo [OK] FACEIT is enabled. Restart Windows, then start FACEIT.
pause
