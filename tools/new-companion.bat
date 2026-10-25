@echo off
rem Creates a new companion folder from tools\companion-template\.
rem Usage: tools\new-companion.bat <id> [companions-root]
rem   <id>              folder name = companion id, [a-z0-9_]+ ("_shared" is reserved: an old install may still have it)
rem   [companions-root] default: module\ModuleData\Companions next to this script's repo
setlocal EnableExtensions

set "HERE=%~dp0"
set "ID=%~1"
set "ROOT=%~2"
if "%ROOT%"=="" set "ROOT=%HERE%..\module\ModuleData\Companions"

if "%ID%"=="" (
  echo usage: %~nx0 ^<id^> [companions-root] 1>&2
  exit /b 2
)
rem findstr ranges like [a-z] also match uppercase letters, so the allowed characters are listed one by one.
echo %ID%| findstr /r /x "[abcdefghijklmnopqrstuvwxyz0123456789_]*" >nul
if errorlevel 1 (
  echo error: id '%ID%' must match [a-z0-9_]+ ^(lowercase letters, digits, '_'^) 1>&2
  exit /b 1
)
if /i "%ID%"=="_shared" (
  echo error: '_shared' is reserved ^(old player-lines folder^) 1>&2
  exit /b 1
)
if exist "%ROOT%\%ID%" (
  echo error: %ROOT%\%ID% already exists; nothing was changed 1>&2
  exit /b 1
)

if not exist "%ROOT%" mkdir "%ROOT%"
xcopy "%HERE%companion-template" "%ROOT%\%ID%\" /e /i /q >nul
if errorlevel 1 (
  echo error: copying the template failed 1>&2
  exit /b 1
)
echo Created %ROOT%\%ID%
echo Next: edit companion.json and lang\en.json, add audio\^<line id^>.ogg for voiced lines.
endlocal
