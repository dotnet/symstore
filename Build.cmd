@echo off
powershell -ExecutionPolicy ByPass .\build\Scripts\Windows\Build.ps1 %*
exit /b %ErrorLevel%
