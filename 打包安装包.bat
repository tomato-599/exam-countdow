@echo off
rem 一键重新打包：生成图标 -> PyInstaller 打包 -> Inno Setup 编译安装包
cd /d "%~dp0"

echo [1/3] 生成图标 app.ico ...
python make_icon.py || goto :err

echo [2/3] 打包主程序（目录版，供安装包使用）...
python -m PyInstaller --noconfirm --clean --windowed --icon app.ico --name GaokaoCountdown main.py || goto :err

echo [3/3] 编译安装包 setup.exe ...
"tools\InnoSetup6\ISCC.exe" installer.iss || goto :err

echo.
echo 打包完成：安装包在 dist-installer\ 目录下
pause
exit /b 0

:err
echo.
echo 打包失败，请查看上面的错误输出。
pause
exit /b 1
