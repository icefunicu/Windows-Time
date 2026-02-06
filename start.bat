@echo off
chcp 65001 >nul
echo ============================================
echo    ScreenTimeWin 屏幕时间管理
echo ============================================
echo.
echo 正在启动应用...

cd /d "%~dp0"

:: 检查是否已编译
if not exist "src\ScreenTimeWin.App\bin\Debug\net8.0-windows\ScreenTimeWin.App.exe" (
    echo 首次运行，正在编译项目...
    dotnet build
    if errorlevel 1 (
        echo 编译失败，请检查环境配置
        pause
        exit /b 1
    )
)

:: 直接运行App（内置本地监控，无需后台服务）
start "" "src\ScreenTimeWin.App\bin\Debug\net8.0-windows\ScreenTimeWin.App.exe"

echo 应用已启动！
