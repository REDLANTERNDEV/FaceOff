@echo off
:: FaceOff fallback script: re-enable FACEIT anti-cheat.
:: Sets both to manual start (demand). Unlike FaceOff.exe, this script cannot remember
:: FACEIT's original start type; check yours once with "sc qc faceit" and edit if needed.

net session >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Run this script as administrator: right-click ^> Run as administrator.
    pause
    exit /b 1
)

echo Enabling faceit kernel driver...
sc config faceit start= demand
net start faceit >nul 2>&1

echo Enabling FACEITService...
sc config FACEITService start= demand
net start FACEITService >nul 2>&1

echo.
echo [OK] FACEIT is enabled. Start the FACEIT client.
echo      If it asks for a restart, restart Windows once.
pause
