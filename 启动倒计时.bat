@echo off
rem Launch GaokaoCountdown without a console window.
cd /d "%~dp0"
where pythonw >nul 2>nul
if %errorlevel%==0 (
    start "" pythonw "%~dp0main.py"
) else (
    start "" python "%~dp0main.py"
)
exit /b 0
