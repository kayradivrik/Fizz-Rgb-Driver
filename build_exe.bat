@echo off
setlocal
cd /d "%~dp0"
echo ============================================================
echo   Redragon K617 RGB ^& Macro Studio - Native EXE Derleyici
echo ============================================================

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set WPF="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"

echo [1/2] RedragonK617_Studio.exe derleniyor...
%CSC% /nologo /target:winexe /out:RedragonK617_Studio.exe /win32icon:icon.ico /optimize+ ^
  /r:%WPF%\PresentationFramework.dll ^
  /r:%WPF%\PresentationCore.dll ^
  /r:%WPF%\WindowsBase.dll ^
  /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll" ^
  /r:System.Drawing.dll ^
  /r:System.Windows.Forms.dll ^
  /r:System.Web.Extensions.dll ^
  src\DriverCore.cs src\EffectsAndMacros.cs src\StudioMainWindow.cs

if %ERRORLEVEL% NEQ 0 (
    echo [HATA] RedragonK617_Studio.exe derlenemedi!
    pause
    exit /b 1
)

echo [2/2] RedragonK617_Setup.exe kurulum paketi derleniyor...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'; " ^
  "$wpf = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF'; " ^
  "$res = @('/resource:RedragonK617_Studio.exe,RedragonK617_Studio.exe', '/resource:icon.ico,icon.ico', '/resource:start.bat,start.bat', '/resource:app.py,app.py', '/resource:k617_driver.py,k617_driver.py', '/resource:effects_engine.py,effects_engine.py', '/resource:macro_engine.py,macro_engine.py', '/resource:settings_manager.py,settings_manager.py'); " ^
  "if (Test-Path 'settings.json') { $res += '/resource:settings.json,settings.json' }; " ^
  "if (Test-Path 'macros.json') { $res += '/resource:macros.json,macros.json' }; " ^
  "Get-ChildItem 'web\*' | ForEach-Object { $res += \"/resource:$($_.FullName),web_$($_.Name)\" }; " ^
  "Get-ChildItem 'templates\*.json' | ForEach-Object { $res += \"/resource:$($_.FullName),tpl_$($_.Name)\" }; " ^
  "Get-ChildItem 'profiles\*.json' | ForEach-Object { $res += \"/resource:$($_.FullName),prf_$($_.Name)\" }; " ^
  "& $csc /nologo /target:winexe /out:RedragonK617_Setup.exe /win32icon:icon.ico /optimize+ /r:\"$wpf\PresentationFramework.dll\" /r:\"$wpf\PresentationCore.dll\" /r:\"$wpf\WindowsBase.dll\" /r:\"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll\" /r:System.Drawing.dll /r:System.Windows.Forms.dll @res src\SetupWizard.cs"

echo ============================================================
echo   Derleme tamamlandi:
copy /Y RedragonK617_Studio.exe FizzCtl.exe >nul
copy /Y RedragonK617_Setup.exe FizzCtl_Setup.exe >nul
echo   - FizzCtl.exe / RedragonK617_Studio.exe (Standalone Uygulama)
echo   - FizzCtl_Setup.exe / RedragonK617_Setup.exe (Kurulum Paketi)
echo ============================================================
