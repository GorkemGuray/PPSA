@echo off
echo Installing PPSA Service...
echo.

REM Check for administrator privileges
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo This script requires administrator privileges.
    echo Please run as administrator.
    pause
    exit /b 1
)

REM Stop and remove existing service if it exists
sc stop PPSA
sc delete PPSA
timeout /t 2 /nobreak >nul

REM Install the service
sc create PPSA binPath= "%~dp0PPSA.exe" start= auto DisplayName= "PLC Process Shutdown Application"
if %errorLevel% neq 0 (
    echo Failed to create service.
    pause
    exit /b 1
)

REM Configure service description
sc description PPSA "Monitors PLC tags and manages system shutdown based on conditions"

REM Configure delayed start
sc config PPSA start= delayed-auto

REM Configure recovery options (restart on failure)
sc failure PPSA reset= 0 actions= restart/60000/restart/60000/restart/60000

REM Start the service
echo Starting PPSA service...
sc start PPSA
if %errorLevel% neq 0 (
    echo Failed to start service.
) else (
    echo Service started successfully.
)

echo.
echo Installation complete. Press any key to exit.
pause
