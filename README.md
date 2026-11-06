# Isdlc

ISDLC sürecini yöneten, şirket içi iş analizi platformu (MVP).
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

### Yapay zekâ servisi (isteğe bağlı)

Tutanak formundaki "Yapay zekâ ile karar ve aksiyonları çıkar" butonu bu servisle çalışır. Servis kapalıyken buton
görünmez; uygulamanın geri kalanı etkilenmez. `ai/.env.example` dosyasını `ai/.env` adıyla kopyalayıp
`GEMINI_API_KEY` değerini gir, sonra servisi Docker ile başlat:

```bash
docker compose --profile ai up -d --build
```

Python ile lokalde çalıştırma ve ayrıntılar: [ai/README.md](ai/README.md). Yalnızca sahte veriyle kullan.
