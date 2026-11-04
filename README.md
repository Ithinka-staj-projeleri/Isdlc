# Isdlc

ISDLC sürecini yöneten, şirket içi iş analizi platformu (MVP).
Proje kuralları için [AGENTS.md](AGENTS.md), plan ve iş dağılımı için [PLANNING.md](PLANNING.md).

## Gerekenler

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Node.js](https://nodejs.org/) (frontend için)

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

API'yi çalıştır (veritabanı açık olmalı):

```bash
dotnet run --project backend/src/Isdlc.Api
```

Swagger sayfası: http://localhost:5091/swagger

Geliştirme ortamında API açılırken migration'lar uygulanır ve seed verisi (süreç akışı, kullanıcılar, örnek projeler)
boş tablolara yüklenir. Veritabanını sıfırdan kurmak için `docker compose down -v` ve `docker compose up -d`,
ardından API'yi yeniden başlat.
