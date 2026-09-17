@echo off
setlocal
cd /d "%~dp0"

if not exist dist mkdir dist

set "CSC32=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "CSC64=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

echo ============================================
echo Hancom HWP/HWPX Merge Tool - Build
echo ============================================
echo.

if exist "%CSC32%" goto BUILD32
goto TRY64

:BUILD32
echo Building 32-bit EXE...
"%CSC32%" /nologo /target:winexe /platform:x86 /optimize+ /win32manifest:app.manifest /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:Microsoft.CSharp.dll /out:"dist\HancomMerge.exe" "HwpMergeDragDrop.cs"
if errorlevel 1 goto BUILD_FAIL
echo.
echo SUCCESS: dist\HancomMerge.exe
echo.
goto OPEN_DIST

:TRY64
if exist "%CSC64%" goto BUILD64
goto NO_CSC

:BUILD64
echo 32-bit compiler not found. Building 64-bit EXE...
"%CSC64%" /nologo /target:winexe /platform:x64 /optimize+ /win32manifest:app.manifest /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:Microsoft.CSharp.dll /out:"dist\HancomMerge_x64.exe" "HwpMergeDragDrop.cs"
if errorlevel 1 goto BUILD_FAIL
echo.
echo SUCCESS: dist\HancomMerge_x64.exe
echo.
goto OPEN_DIST

:OPEN_DIST
start "" explorer.exe "%CD%\dist"
echo Press any key to close this window.
pause >nul
exit /b 0

:NO_CSC
echo.
echo ERROR: C# compiler was not found.
echo Checked:
echo %CSC32%
echo %CSC64%
echo.
echo Please send me a screenshot of this window.
pause
exit /b 1

:BUILD_FAIL
echo.
echo ERROR: Build failed.
echo Please copy all error lines shown above and send them to me.
pause
exit /b 1
