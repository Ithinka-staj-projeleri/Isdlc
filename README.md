# Isdlc

ISDLC sürecini yöneten iş analizi platformu. Birden fazla şirket, her biri kendi kullanıcıları, akışları ve kayıtlarıyla
aynı uygulamayı kullanabilir (Faz 6; kurallar [docs/multi-tenancy.md](docs/multi-tenancy.md)).
Proje kuralları için [AGENTS.md](AGENTS.md), plan ve iş dağılımı için [PLANNING.md](PLANNING.md).

## Gerekenler

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Node.js](https://nodejs.org/) (frontend için)
- [Python 3.11+](https://www.python.org/) (isteğe bağlı: yapay zekâ servisi için)

## Lokalde çalıştırma

### 1. Veritabanı (MSSQL, Docker)

İlk seferde ortam dosyasını oluştur:

```bash
cp .env.example .env
```

Veritabanını başlat (Docker Desktop açık olmalı):

```bash
docker compose up -d
```

Durumunu kontrol et, `STATUS` sütununda `healthy` görünmeli:

```bash
docker compose ps
```

Durdurmak için `docker compose down`. Veriler `isdlc-mssql-data` volume'ünde kalır;
veritabanını tamamen sıfırlamak için `docker compose down -v`.

> 1433 portu bilgisayarında doluysa `.env` içindeki `MSSQL_PORT` değerini değiştir.

### 2. Backend

İlk seferde repo kökünde proje araçlarını (`dotnet-ef`) kur:

```bash
dotnet tool restore
```
İlk seferde JWT anahtarını ve test parolasını kendi bilgisayarına tanıt. Bu değerler repoya yazılmaz.
Anahtar yoksa API açılmaz:

```bash
dotnet user-secrets set "Jwt:SigningKey" "<en az 32 karakterlik rastgele bir metin>" --project backend/src/Isdlc.Api
dotnet user-secrets set "Identity:TestPassword" "<ekipçe belirlenen test parolası>" --project backend/src/Isdlc.Api
```

Rastgele anahtarı PowerShell ile üretebilirsin:

```powershell
([guid]::NewGuid().ToString("N") + [guid]::NewGuid().ToString("N"))
```

Test parolası bütün seed kullanıcılarının (örneğin `elif.yilmaz@isdlc.local`) parolası olur. Parola, API ilk
açıldığında kullanıcının parolası boşsa yazılır. Parolayı sonradan değiştirirsen veritabanını sıfırla
(`docker compose down -v`, sonra `docker compose up -d`). API açıkken Swagger'da önce `POST /api/v1/auth/login`
ile token al, sonra sağ üstteki "Authorize" düğmesine yapıştır. Girişsiz hiçbir uç nokta çalışmaz.

API'yi çalıştır (veritabanı açık olmalı):

```bash
dotnet run --project backend/src/Isdlc.Api
```

Swagger sayfası: http://localhost:5091/swagger

Testleri çalıştır (Docker açık olmalı; testler kendi SQL Server konteynerini açıp kapatır):

```bash
dotnet test backend/Isdlc.sln
```

Geliştirme ortamında API açılırken migration'lar uygulanır ve seed verisi (süreç akışı, kullanıcılar, örnek projeler)
boş tablolara yüklenir. Veritabanını sıfırdan kurmak için `docker compose down -v` ve `docker compose up -d`,
ardından API'yi yeniden başlat.

### 3. Frontend

```bash
cd frontend
npm install
npm start
```

Uygulama: http://localhost:4200. API'ye giden istekler geliştirme sunucusu üzerinden 5091'e yönlendirilir.

### Yapay zekâ servisi (isteğe bağlı)

Tutanak formundaki "Yapay zekâ ile karar ve aksiyonları çıkar" butonu bu servisle çalışır. Servis kapalıyken buton
görünmez; uygulamanın geri kalanı etkilenmez. `ai/.env.example` dosyasını `ai/.env` adıyla kopyalayıp
`GEMINI_API_KEY` değerini gir, sonra servisi Docker ile başlat:

```bash
docker compose --profile ai up -d --build
```

Python ile lokalde çalıştırma ve ayrıntılar: [ai/README.md](ai/README.md). Yalnızca sahte veriyle kullan.

## Yeni şirket açma

Uygulamayı kullanan her şirketin kendi kullanıcıları, akış kopyaları, soru listeleri ve kayıtları vardır; bir şirket
başkasının hiçbir kaydını göremez. Geliştirme ortamında hazır gelen şirket **Ithinka**'dır (seed kullanıcıları ve örnek
projeler onundur).

**1. Platform Yöneticisi girer.** Şirketleri yalnızca Platform Yöneticisi açar; başka bir işi yoktur, şirketlerin kayıtlarını
göremez.
- Geliştirme ortamında API açılırken `platform@isdlc.local` adıyla, test parolasıyla oluşturulur.
- Başka ortamda yapılandırmayla verilir: `Platform:AdminEmail` ve `Platform:AdminPassword`. Bu parola ilk girişte değiştirilir.

**2. Şirketi açar.** Menü → **Şirketler** → **+ Yeni şirket** (`/platform/tenants`):
- Şirket adı ve kodu (küçük harf, rakam ve tire; sonradan değişmez).
- Şirketin ilk yöneticisi: ad soyad, e-posta ve geçici parola (form üretir; "Yeni üret" ile değişir). Yönetici yetkisiz
  açılır; yetkileri kendi ekranından verir (adım 3).
- **Şirketi aç** denince şirkete beş akış, doküman türleri ve soru listeleri şablondan kopyalanır. Geçici parola ekranda bir
  kez gösterilir; kişiye uygulama dışından iletilir.

**3. Şirket Yöneticisi ilk kez girer.** Geçici parolayla girince parola değiştirme ekranı açılır; yeni parola en az 12
karakterdir. Sonra menüde **Yönetim** bölümünü görür:
- **Kullanıcılar** (`/admin/users`): kullanıcı ekler (geçici parolayla), yetki ve şirket yöneticisi işareti verir, pasife
  alır, yeniden geçici parola verir. Kullanıcı silinmez. Yetkiler aşağıda.
- **Talep Alanları** (`/admin/request-fields`): şirketin talep formuna kendi alanlarını ekler (metin, sayı, tarih, seçim,
  evet / hayır; zorunlu ya da değil). Alan silinmez, pasife alınır; pasif alan formda görünmez, projedeki değeri saklı kalır.
- **Şirket Ayarları** (`/admin/settings`): yapay zekâ yardımcıları ve metinlerin dış modele gönderilme izni. Yeni şirkette
  ikisi de kapalıdır; ne anlama geldikleri ekranda yazar.

**4. Kullanıcılar çalışmaya başlar.** Proje numaraları ve kayıt kodları her şirkette 1'den başlar.

### Yetkiler

Akıştaki işleri kimin yapacağını yetkiler belirler (Faz 7 · A2; eski unvanların yerini aldı). Liste sabittir ve her şirkette
aynıdır; bir kişiye istenen sayıda yetki verilebilir:

| Yetki | Ne için |
| --- | --- |
| Analiz adımları | Analiz adımları; talep açmak; projenin iş analisti olmak |
| Proje yönetimi adımları | Proje yönetimi adımları; projenin proje yöneticisi olmak |
| Onay ve karar | Onay adımları; talebi reddetmek, ertelemek, değerlendirmeye geri almak |
| Teklif ve anlaşma | Teklif ve anlaşma adımları |
| Geliştirme | Geliştirme adımları |
| Test | Test adımları |

- **Yalnızca akış işleri yetki ister.** Risk, açık konu, tutanak, gereksinim, doküman, formlar, panel, arama ve yapay zekâ
  yardımcıları için yetki gerekmez; yetkisiz kişi bunları yapabilir.
- **Hazır setler:** Kullanıcı formundaki "Hazır set" eski unvanları seçer (ör. İş Analisti → Analiz adımları). Set
  kaydedilmez; yalnızca kutuları işaretler.
- **Yetki eksikse** ekran düğmeyi kapatır ve hangi yetkinin gerektiğini, kimden isteneceğini yazar: "Bu adım için «Analiz
  adımları» yetkisi gerekir. Şirket yöneticinizden isteyin: Mert Kaya." API de aynı metinle 403 döner.
- **Değişiklik hemen geçerli olur.** Yetkiler token'a yazılmaz; yönetici değiştirince kişinin bir sonraki isteği yeni
  yetkiyle çalışır, yeniden giriş gerekmez.
- **Şirket Yöneticisi** bir yetki değil işarettir: kullanıcıları, talep alanlarını ve şirket ayarlarını yönetir. Akışta iş
  yapacaksa ona da yetki verilir. Platform Yöneticisinin şirketi ve yetkisi yoktur.

Kurallar ve uç noktalar: [docs/configurability.md](docs/configurability.md) → Yetkiler.

Ithinka'nın Şirket Yöneticisi seed'den gelir: `mert.kaya@isdlc.local` (`Seed/users.json` → `isTenantAdmin`). Eski bir
veritabanında şirketin hiç aktif yöneticisi yoksa API açılırken bu kullanıcı yönetici yapılır.

Şirketi pasife almak için Platform Yöneticisi şirketler listesinde **Düzenle**'yi kullanır; o şirketin kullanıcıları giriş
yapamaz, açık oturumları en geç bir dakika içinde kapanır.

API ile aynı işler: `POST /api/v1/tenants`, `POST /api/v1/users`, `PUT /api/v1/tenants/current/settings`
([docs/multi-tenancy.md](docs/multi-tenancy.md) → Uç noktalar).
