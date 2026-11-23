@echo off
title SysDash - Kaldirma / Uninstall
echo.
echo SysDash kapatiliyor...

rem Calisan kopyalari kapat (yonetici olarak acilmis olanlar dahil)
taskkill /F /IM SysDash.exe >nul 2>&1
powershell -NoProfile -Command "Get-Process SysDash -ErrorAction SilentlyContinue | ForEach-Object { $_.Kill() }" >nul 2>&1

echo Otomatik baslatma girdisi kaldiriliyor...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "SysDash" /f >nul 2>&1

echo.
echo SysDash kaldirildi.
echo Ayarlar (%%APPDATA%%\SysDash) korunuyor. Tamamen temizlemek icin:
echo    rmdir /s /q "%%APPDATA%%\SysDash"
echo.
echo Bu klasoru (kurulum dizinini) artik silebilirsin.
echo.
pause
