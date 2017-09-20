
@echo off

.paket\paket.exe install
if errorlevel 1 (
  exit /b %errorlevel%
)

setlocal

packages\build\FAKE\tools\FAKE.exe build.fsx %*

endlocal
