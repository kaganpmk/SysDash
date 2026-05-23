# SysDash

SysDash sistem ve ağ izleme arayüzüdür — ekran üzerinde CPU, GPU, bellek, ağ hızları ve çalışma süresi bilgilerini gösterir.

**Öne çıkanlar**

- Gerçek zamanlı CPU / GPU / RAM / Ağ metrikleri
- Çoklu monitör desteği ve küçük ekran modları
- Arka plan uygulama ve sistem tepsisi kısayolları

---

## İndirme ve Kurulum

Hazır paketler ve kurulum dosyaları GitHub Releases sayfasında bulunur. Her release içinde iki varlık olacak:

- `SysDash-<tag>.zip` — tek dosya olarak yayınlanan self-contained uygulama (çalıştırılabilir exe içerir)
- `SysDashSetup-<tag>.exe` — Windows için kurulum programı (Inno Setup ile oluşturulmuş)

Kurulum seçenekleri:

- Hızlı (portable): `SysDash-<tag>.zip` içindeki `SysDash.exe`'yi çıkarıp çalıştırın.
- Kurulum: `SysDashSetup-<tag>.exe`'yi çift tıklayıp standart Windows kurulum adımlarını takip edin.

Not: Releases sayfası repo ana sayfasında "Releases" veya "Tags" bölümünde görünür; kullanıcılar oradan uygun `v*` tag'ini seçip dosyaları indirebilir.

---

## Güncelleme akışı (kullanıcı olarak)

1. Siz bir yeni sürüm (ör. `v1.2.0`) görmek isterseniz, repository sahibi bu sürümü tag'leyip GitHub'a pushlar.
2. Tag pushlandığında GitHub Actions otomatik olarak derler ve release'e `SysDash-<tag>.zip` ile `SysDashSetup-<tag>.exe` yükler.
3. Kullanıcılar Releases'ten dilediklerini indirip kurabilirler. Eski sürümler release listesinde kalır; kullanıcılar seçim yapabilir.

---

## Sorun bildirimi / İletişim

Hataları Issues sekmesinden bildirin veya repo sahibiyle doğrudan iletişime geçin.

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
