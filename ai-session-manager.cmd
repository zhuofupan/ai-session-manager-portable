@echo off
setlocal
chcp 65001 >nul
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File "%~dp0tools\ai-session-manager.ps1" %*
