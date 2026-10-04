@echo off
rem Install the only dependency: PyQt6
cd /d "%~dp0"
python -m pip install -r requirements.txt
echo.
echo Done. Press any key to close.
pause >nul
