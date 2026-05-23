# SysDash — Repo Yönergeleri

Bu dosya, projeyi GitHub'a yüklerken hangi dosyaların dahil edilmesi/edilmemesi gerektiğini ve basit bir yayın (release) / indirme bağlantısı oluşturma akışını açıklar.

**Hangi dosyaları yüklemeliyim?**

- **Ekle**: çözüm dosyası ve kaynak kod: `SysDash.slnx`, `SysDash.csproj`, `App.xaml`, `MainWindow.xaml`, `Controls/`, `Converters/`, `Services/`, `ViewModels/`, `Models/`, `Resources/`
- **Ekle**: proje ayarları ve bağımlılık tanımları (ör. paket referansları) — `.csproj` dosyaları.

**Hangi dosyaları yüklememeliyim?**

- **Hariç tut**: derleme çıktıları ve ara dosyalar — `bin/`, `obj/`.
- **Hariç tut**: IDE ve kullanıcı ayarları — `.vs/`, `.vscode/`, `*.user`, `*.suo`.
- **Hariç tut**: NuGet paketleri ve paket çıktıları — `packages/`, `*.nupkg`.
- **Hariç tut**: yerel/secret yapılandırma dosyaları — `appsettings.*.json`, `*.local`.

## Basit indirme / setup seçenekleri

1. Hızlı yol — Self-contained single-file publish (zip olarak paylaşmak)

- Yerel olarak üretmek için (PowerShell, proje kökünde):

```powershell
dotnet publish SysDash/SysDash.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o publish
Compress-Archive -Path publish\* -DestinationPath SysDash-win-x64.zip
```

- Ortaya çıkan `SysDash-win-x64.zip` dosyasını GitHub Release'e yükleyin; kullanıcılar zip'i indirip çalıştırabilir.

2. Resmi bir yükleyici oluşturmak

- MSIX veya MSI için: WiX Toolset veya Advanced Installer kullanabilirsiniz.
- Kolay seçenek: Inno Setup ile bir installer script yazıp `artifact` olarak paylaşmak.

3. Otomatik derleme + release (GitHub Actions)

- Bu repoya bir GitHub Actions iş akışı ekleyerek, belirli bir tag (`v*`) ile pusha çıktığınızda otomatik olarak derleyip release oluşturabilirsiniz. Aşağıda `.github/workflows/publish.yml` örneği bulunur.

---

İsterseniz ben `.gitignore`, temel `README.md` ve basit bir GitHub Actions iş akışı dosyası ekleyebilirim ve isterseniz commit/push işlemlerini nasıl yapacağınızı gösteririm.

## Nasıl release yaparım (adımlar)

1. Değişiklikleri commit & push edin:

```bash
git add .
git commit -m "Your message"
git push origin main
```

2. Yeni release oluşturmak için bir tag oluşturup pushlayın (CI bu tag ile tetiklenir):

```bash
git tag v1.0.0
git push origin v1.0.0
```

Bu repoda GitHub Actions yalnızca `v*` etiketli push'larda release oluşturacak şekilde yapılandırıldı. Tag pushladığınızda CI:

- `dotnet publish` ile `publish/` içine self-contained single-file üretir
- `artifact.zip` oluşturur ve release'e yükler
- Inno Setup (CI üzerinde yüklü) kullanılarak `SysDashSetup.exe` oluşturulur ve release'e yüklenir

3. Yerel olarak test etmek isterseniz:

```powershell
dotnet publish SysDash/SysDash.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o publish
Compress-Archive -Path publish\* -DestinationPath SysDash-win-x64.zip
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Installer\SysDash.iss /F"SysDashSetup"
```

Alternatif: GitHub web arayüzünden bir release oluşturup `artifact.zip` ve `SysDashSetup.exe` dosyalarını manuel olarak da yükleyebilirsiniz.
