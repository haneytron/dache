CLS
@echo off
SETLOCAL

TITLE Dache Host Uninstaller

SET INSTALL_UTIL=%windir%\Microsoft.Net\Framework\v4.0.30319\installutil.exe
SET SERVICE_EXE=%~d0%~p0\Dache.CacheHostService.exe

if not exist "%INSTALL_UTIL%" (
    echo FAILURE: Could not find installutil.exe (path searched: "%INSTALL_UTIL%"^). Exiting...
    pause
    goto:eof
)

if not exist "%SERVICE_EXE%" (
    echo FAILURE: Could not find Cache Host Service (path searched: "%SERVICE_EXE%"^). Exiting...
    pause
    goto:eof
)

echo INFO: Administrative permissions required. Detecting permissions...
net session >nul 2>&1
if %errorLevel% == 0 (
    echo SUCCESS: Administrative permissions confirmed.
) else (
    echo FAILURE: Current permissions inadequate. Please run as administrator. Exiting...
    pause
    goto:eof
)


echo INFO: Uninstalling Dache Cache Host
echo.

%INSTALL_UTIL% -u "%SERVICE_EXE%"

echo.
if %errorlevel% == 0 (
    echo SUCCESS: Dache Cache Host has been successfully uninstalled.
) else (
    echo FAILURE: Dache Cache Host could not be uninstalled.
)

pause

ENDLOCAL