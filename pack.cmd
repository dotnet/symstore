@echo off
powershell -ExecutionPolicy ByPass %~dp0build\Build.ps1 -pack %*
exit /b %ErrorLevel%
