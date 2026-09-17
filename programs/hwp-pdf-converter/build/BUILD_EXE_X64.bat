@echo off
setlocal
cd /d "%~dp0"

if not exist dist mkdir dist
set "CSC64=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if not exist "%CSC64%" goto NO_CSC

echo Building 64-bit EXE...
"%CSC64%" /nologo /target:winexe /platform:x64 /optimize+ /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:Microsoft.CSharp.dll /out:"dist\HancomPdfBatch_x64.exe" "src\Program.cs"
if errorlevel 1 goto BUILD_FAIL

echo.
echo SUCCESS: dist\HancomPdfBatch_x64.exe
start "" explorer.exe "%CD%\dist"
pause >nul
exit /b 0

:NO_CSC
echo ERROR: 64-bit C# compiler was not found.
pause
exit /b 1

:BUILD_FAIL
echo ERROR: Build failed.
pause
exit /b 1
