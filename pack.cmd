@echo off
powershell -ExecutionPolicy ByPass %~dp0eng\common\Build.ps1 -pack %*
exit /b %ErrorLevel%
