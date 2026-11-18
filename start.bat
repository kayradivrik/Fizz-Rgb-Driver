@echo off
cd /d "%~dp0"

if "%~1"=="--python" goto :run_py
if "%~1"=="--browser" goto :run_py_browser

if exist "RedragonK617_Studio.exe" (
    start "" "RedragonK617_Studio.exe" %* 2>nul
    if %ERRORLEVEL% EQU 0 exit /b 0
)

:run_py
where pythonw >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    start "" pythonw app.py %*
    exit /b 0
)
start "" python app.py %*
exit /b 0

:run_py_browser
python app.py --browser
