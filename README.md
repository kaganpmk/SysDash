# SysDash

![Release](https://img.shields.io/github/v/release/kaganpmk/SysDash?style=flat-square&color=a9f1fe&labelColor=0d0f14)
![License](https://img.shields.io/github/license/kaganpmk/SysDash?style=flat-square&color=a9f1fe&labelColor=0d0f14)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0d0f14?style=flat-square&color=a9f1fe)

[English](#english) · [Türkçe](#türkçe)

Gercek zamanli sistem izleyici. CPU, GPU, VRAM, RAM, ag hizi, ping, disk dolulugu ve calisma suresini tek panelde gosterir.

---

## Türkçe

### Kurulum

**1. Yol — Kurulum dosyasi (onerilen)**

[**SysDashSetup.exe indir**](https://github.com/kaganpmk/SysDash/releases/latest/download/SysDashSetup.exe) → calistir → bitti.

- Windows 10/11 x64. **.NET kurulumu gerekmez** (self-contained).
- Otomatik olarak `%LOCALAPPDATA%\Programs\SysDash` altina kurulur.
- Masaüstü kisayolu ve "Windows ile baslat" secenekleri kurulumda sorulur.
- Kaldirma: **Başlat menüsü → SysDash → SysDash kaldir** ya da Windows Ayarlar → Uygulamalar.

**2. Yol — Taşınabilir (portable)**

[**SysDash.exe indir**](https://github.com/kaganpmk/SysDash/releases/latest/download/SysDash.exe) → istedigin klasore koy → cift tikla. Kurulum yapmaz.

Taşınabilir kullanımda kaldırmak için: `Uninstall.bat` çalıştır (otomatik başlatma kaydını siler), sonra klasörü sil.

> **SmartScreen notu:** Uygulama kod imzalı değil. Ilk açılışta "Windows korumalı bilgisayarınızı korudu" görürsen **Ek bilgi → Yine de çalıştır**.

### Ne gosterir

| Kart | Icerik |
|---|---|
| **CPU** | Yük yüzdesi, sicaklik, ince doluluk çubugu |
| **GPU** | Yük, sicaklik, VRAM (kullanilan/toplam + yüzde) |
| **RAM** | Kullanilan/toplam GB, yüzde halkasi (gauge ortasinda) |
| **AĞ** | İndirme/yükleme hizi, hedefe göre ping |
| **Alt şerit** | C:, D: ve **harfsiz Linux bölümleri** dahil disk dolulugu + sistem açik kalma süresi |
| **Başlık** | Saat, tarih, gün |

### Kisayollar ve ayarlar

- **Tepsi ikonu** (sağ alt): **Ekran Seç** · **Görünüm** (Tam Ekran Panel / Küçük Pencere) · **Başlangıçta Aç** · **Çıkış**
- Küçük pencere varsayilan **800×600**; sürükleyip boyutlandirdığında konum hatirlanir.
- Ayarlar: `%APPDATA%\SysDash\config.json`
- Gunluk: `%APPDATA%\SysDash\startup.log` (sorun bildirirken bunu ekle)

### Yetki ve sinirlar

- **CPU sicakligi** yönetici izni **gerektirmez** (perf-counter kaynağindan okunur). WMI ACPI yolu daha hassastir ama yönetici ister.
- **Harfsiz Linux bölümleri** (ext2/3/4) için doluluk okumak yönetici izni ister; izin verilmezse bölüm yine listelenir, sadece toplam boyut yazar.
- Ping hedefleri `config.json` → `PingTargets` (varsayilan `1.1.1.1`, `8.8.8.8`).

### Gelistirme

```bash
dotnet run --project SysDash/SysDash.csproj
```

Gereksinimler: .NET 8 SDK, Windows 10/11.

### Kurulum dosyasini kendin uretmek

```powershell
# 1) Yayinla
dotnet publish SysDash/SysDash.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish

# 2) Inno Setup 6 ile kurulumu derle
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Installer\SysDash.iss
# Cikti: Installer\Output\SysDashSetup.exe
```

### Lisans

MIT — [LICENSE](LICENSE).

---

## English

Real-time system monitor: CPU, GPU, VRAM, RAM, network throughput, ping, disk usage and uptime in one panel.

### Install

**Option 1 — Installer (recommended)**

[**Download SysDashSetup.exe**](https://github.com/kaganpmk/SysDash/releases/latest/download/SysDashSetup.exe) → run it → done.

- Windows 10/11 x64, **no .NET install required** (self-contained).
- Installs to `%LOCALAPPDATA%\Programs\SysDash`.
- Desktop shortcut and auto-start are optional during setup.
- Uninstall: **Start menu → SysDash → Uninstall SysDash**, or Windows Settings → Apps.

**Option 2 — Portable**

[**Download SysDash.exe**](https://github.com/kaganpmk/SysDash/releases/latest/download/SysDash.exe) → put it anywhere → double-click. Nothing gets installed.

Run `Uninstall.bat` in that folder to remove the auto-start entry, then delete the folder.

> **SmartScreen note:** The app is not code-signed. If Windows shows a warning on first launch, choose **More info → Run anyway**.

### What it shows

- **CPU** — load %, temperature, thin progress bar
- **GPU** — load, temperature, VRAM (used/total + %)
- **RAM** — used/total GB, percentage arc drawn inside the gauge
- **Network** — download/upload speed and ping
- **Footer** — disk usage for all volumes **including unmounted Linux partitions**, plus uptime

### Permissions

- CPU temperature works **without** admin rights (perf-counter source).
- Reading free space on **unmounted Linux partitions** requires admin; without it the partition is still listed with its total size only.

### Build

```bash
dotnet publish SysDash/SysDash.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Installer\SysDash.iss
```

### License

MIT — see [LICENSE](LICENSE).
