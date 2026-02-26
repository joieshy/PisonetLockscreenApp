@echo off
echo ======================================================
echo Pisonet Lockscreen App - Full EXE Build Script
echo ======================================================

REM Set paths
set "DEPLOY_DIR=Deployment"
set "APP_PUBLISH_DIR=bin\Release\net9.0-windows\win-x64\publish"
set "WATCHDOG_PUBLISH_DIR=PisonetWatchdog\bin\Release\net9.0-windows\win-x64\publish"

echo [1/4] Cleaning previous builds...
if exist "%DEPLOY_DIR%" rd /s /q "%DEPLOY_DIR%"
mkdir "%DEPLOY_DIR%"

echo [2/4] Publishing PisonetLockscreenApp...
dotnet publish PisonetLockscreenApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o "%DEPLOY_DIR%"
if %ERRORLEVEL% NEQ 0 (
    echo Error: Failed to publish Main App.
    pause
    exit /b %ERRORLEVEL%
)

echo [3/4] Publishing PisonetWatchdog...
REM We publish to a temp folder first to avoid overwriting the main app's files if they share names
dotnet publish PisonetWatchdog.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "temp_watchdog"
if %ERRORLEVEL% NEQ 0 (
    echo Error: Failed to publish Watchdog.
    pause
    exit /b %ERRORLEVEL%
)

echo [4/4] Finalizing Deployment folder...
copy /y "temp_watchdog\PisonetWatchdog.exe" "%DEPLOY_DIR%\"
rd /s /q "temp_watchdog"

REM Ensure media folder is copied (dotnet publish usually handles this if set in csproj, but we double check)
if exist "media" (
    echo Copying media folder...
    xcopy /y /s /e "media\*" "%DEPLOY_DIR%\media\"
)

echo.
echo ======================================================
echo Build Complete! 
echo All files are ready in the '%DEPLOY_DIR%' folder.
echo You can now copy the '%DEPLOY_DIR%' folder to any PC.
echo ======================================================
echo.
pause
