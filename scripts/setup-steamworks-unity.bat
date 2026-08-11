@echo off
REM Install Steamworks.NET into Unity Plugins (SB-4180).
REM Usage: scripts\setup-steamworks-unity.bat
REM Optional: set STEAMWORKS_NET_TAG=20.2.0
REM Dev App ID: Spacewar 480. No secrets.
setlocal EnableExtensions
cd /d "%~dp0.."

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup-steamworks-unity.ps1"
exit /b %ERRORLEVEL%
