@echo off
powershell -ExecutionPolicy ByPass %~dp0build\Build.ps1 -verbosity normal -restore -build -test -sign -pack -ci %*
exit /b %ErrorLevel%
