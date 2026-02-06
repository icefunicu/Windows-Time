@echo off
chcp 65001 >nul
echo ============================================
echo    ScreenTimeWin 开发模式启动
echo ============================================
echo.

cd /d "%~dp0"

echo 正在编译并启动...
dotnet run --project src/ScreenTimeWin.App
