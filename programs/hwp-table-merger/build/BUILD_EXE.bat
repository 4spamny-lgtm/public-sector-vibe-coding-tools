@echo off
echo === Building HWPX Table Merge ===
taskkill /f /im HwpxTableMerge.exe >nul 2>&1
if exist build rmdir /s /q build
if exist dist rmdir /s /q dist
python -m pip install lxml tkinterdnd2 pyinstaller
python -m PyInstaller --onefile --windowed --clean --name HwpxTableMerge --collect-all tkinterdnd2 --hidden-import lxml.etree --hidden-import lxml._elementpath hwpx_merge_gui.py
echo.
if exist dist\HwpxTableMerge.exe (echo Done. Distribute: dist\HwpxTableMerge.exe) else (echo BUILD FAILED - see messages above)
pause
