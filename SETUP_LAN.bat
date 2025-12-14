@echo off
:: Run PowerShell script as Administrator
echo Running LAN Access Setup...
echo.

:: Check for admin rights
net session >nul 2>&1
if %errorLevel% == 0 (
    :: Already running as admin
    powershell -ExecutionPolicy Bypass -File "%~dp0setup-lan-access.ps1"
) else (
    :: Request admin rights and run
    powershell -Command "Start-Process PowerShell -ArgumentList '-ExecutionPolicy Bypass -File \"%~dp0setup-lan-access.ps1\"' -Verb RunAs"
)
