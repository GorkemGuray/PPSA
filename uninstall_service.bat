@echo off
echo Uninstalling PPSA Service...
echo.

REM Check for administrator privileges
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo This script requires administrator privileges.
    echo Please run as administrator.
    pause
    exit /b 1
)

REM Stop and remove the service
sc stop PPSA
timeout /t 2 /nobreak >nul
sc delete PPSA

if %errorLevel% neq 0 (
    echo Failed to remove service.
) else (
    echo Service removed successfully.
)

echo.
echo Uninstallation complete. Press any key to exit.
pause
