@echo off
:: FaceOff fallback script: disable FACEIT anti-cheat so it does not load at boot.
:: Run as administrator. A restart is usually needed to unload the kernel driver.

net session >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Run this script as administrator: right-click ^> Run as administrator.
    pause
    exit /b 1
)

echo Closing FACEIT (anti-cheat client and app)...
taskkill /IM faceitclient.exe /F /T >nul 2>&1
:: To keep the FACEIT app open, add "::" at the start of the next line.
taskkill /IM FACEIT.exe /F /T >nul 2>&1

echo Disabling FACEITService...
sc config FACEITService start= disabled
net stop FACEITService >nul 2>&1

echo Disabling faceit kernel driver...
sc config faceit start= disabled
net stop faceit >nul 2>&1

echo.
sc query faceit | find "RUNNING" >nul
if errorlevel 1 (
    echo [OK] FACEIT is off and will not load at boot.
) else (
    echo [OK] FACEIT is disabled, but the driver is still loaded.
    echo      Restart Windows to unload it.
)
pause
