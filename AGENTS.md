# AGENTS.md — Ortak Geliştirme Yönergesi

Bu dosya projede çalışan bütün kodlama ajanları ve geliştiriciler için ortak kaynaktır.
Ajan her oturumun başında bu dosyayı okur. Bir kural değişirse önce bu dosya güncellenir.

## 1. Proje

ISDLC (Yazılım Geliştirme Yaşam Döngüsü) sürecini yöneten, şirket içi kullanılacak bir iş analizi platformu.
Şu anki hedef basit bir **MVP**'dir.

Ürün adı henüz kesin değildir. Kodda kök ad olarak `Isdlc` kullanılır.

## 2. Teknoloji yığını

| Katman | Teknoloji |
| --- | --- |
| Frontend | Angular, TypeScript |
| Backend | ASP.NET Core Web API (.NET) |
| Veritabanı | MSSQL (lokalde Docker ile) |
| Veri erişimi | Entity Framework Core, code-first migrations |
| Kod ve iş takibi | GitHub |

Yığına yeni bir kütüphane veya paket eklenmeden önce geliştiriciye sorulur.

## 3. Planlanan modüller

Aşağıdaki liste kesin değildir; kapsam netleştikçe güncellenir.

**MVP'de yapılması planlananlar**

| Modül | Kod adı | Kısaca |
| --- | --- | --- |
| Giriş ve kullanıcı/rol yönetimi | `Identity` | Kullanıcı girişi, roller |
| Talepler ve projeler | `Projects` | Talep kaydı, proje listesi ve detayı |
| Süreç akışı | `Process` | 13 aşamalı ISDLC adımları, Evet/Hayır kararları, onaylar |
| Seviye ve kabul | `Assessment` | Seviye belirleme, proje kabul puanlaması |
| Doküman takibi | `Documents` | Proje dokümanlarının durumu |
| Gereksinimler | `Requirements` | Gereksinim / User Story kayıtları |
| Risk yönetimi | `Risks` | Risk kaydı, olasılık × etki skoru |
| Açık konular | `OpenIssues` | Soru, aksiyon, karar, değişiklik talebi kayıtları |
| Toplantı tutanakları | `Meetings` | Tutanak, aksiyonların açık konulara aktarılması |
| Gösterge paneli | `Dashboard` | Özet göstergeler |
| Bildirimler | `Notifications` | Uygulama içi bildirimler |
| Denetim kaydı | `Audit` | Yapılan değişikliklerin kaydı |

**Sonraki sürümlere bırakılanlar:** Test & UAT, Teklif & Maliyet, Kaynak Planlama, Faturalama,
Proje Devir, Otomasyon, AI özellikleri.

## 4. Klasör yapısı

```
/
├─ AGENTS.md
├─ README.md
├─ docker-compose.yml
├─ .env.example
├─ backend/
│  ├─ Isdlc.sln
│  ├─ src/
│  │  ├─ Isdlc.Api/              # controller'lar, middleware, Program.cs
│  │  ├─ Isdlc.Application/      # servisler, DTO'lar, doğrulama, iş kuralları
│  │  ├─ Isdlc.Domain/           # entity'ler, enum'lar
│  │  └─ Isdlc.Infrastructure/   # DbContext, EF yapılandırmaları, migrations, seed
│  └─ tests/
│     └─ Isdlc.Application.Tests/
├─ frontend/
│  └─ src/app/
│     ├─ core/                   # kimlik doğrulama, interceptor, guard, sayfa düzeni
│     ├─ shared/                 # ortak bileşenler, pipe'lar
│     └─ features/<modul>/       # her modülün ekranları, servisleri, modelleri
└─ docs/
```

Backend'de her katmanın içinde modül klasörleri bulunur (`Isdlc.Application/Risks/` gibi).
Klasör adları Bölüm 3'teki kod adlarıdır.
Frontend'de `features/` altındaki klasörler aynı modüllerin kebab-case halidir (`open-issues`).

## 5. İsimlendirme kuralları

**Genel**
- Arayüzde görünen bütün metinler **Türkçe**.
- Kod, dosya, tablo, kolon, API yolu ve commit mesajları **İngilizce**.

**Backend (C#)**
| Öğe | Kural | Örnek |
| --- | --- | --- |
| Sınıf, metot, property | PascalCase | `RiskService`, `CalculateScore()` |
| Arayüz | `I` + PascalCase | `IRiskService` |
| Yerel değişken, parametre | camelCase | `projectId` |
| Private alan | `_` + camelCase | `_dbContext` |
| DTO | Amaç + `Dto` | `CreateRiskDto`, `RiskListItemDto` |
| Controller | Çoğul kaynak + `Controller` | `RisksController` |
| Entity | Tekil | `Risk`, `OpenIssue` |
| Enum ve değerleri | PascalCase | `ProjectStatus.Active` |

**Veritabanı**
| Öğe | Kural | Örnek |
| --- | --- | --- |
| Tablo | Çoğul, PascalCase | `Risks`, `OpenIssues` |
| Kolon | PascalCase | `ProjectId`, `CreatedAt` |
| Birincil anahtar | `Id` | `Id` |
| Yabancı anahtar | `<Entity>Id` | `ProjectId` |
| Kullanıcıya gösterilen kod | `Code` kolonu | `ISDLC-2026-014`, `RSK-01` |
| Migration | Ne yaptığını anlatan PascalCase | `AddRisksTable` |

**API**
- Yol: `/api/v1/<kaynak>`, kaynak çoğul ve kebab-case: `/api/v1/open-issues`
- Alt kaynak: `/api/v1/projects/{id}/risks`

**Frontend (Angular)**
| Öğe | Kural | Örnek |
| --- | --- | --- |
| Dosya adı | kebab-case + tür | `risk-list.component.ts`, `risk.service.ts` |
| Sınıf | PascalCase + tür | `RiskListComponent`, `RiskService` |
| Model (interface) | PascalCase, tür eki yok | `Risk`, `OpenIssue` |
| Değişken, metot | camelCase | `loadRisks()` |
| Bileşen seçici | `app-` + kebab-case | `app-risk-list` |

**Git**
- Dal: `feature/<issue-no>-kisa-ad`, `fix/<issue-no>-kisa-ad`
- Commit: Conventional Commits, kapsam modül adı: `feat(risks): add risk score calculation`
- `main` dalına doğrudan push yapılmaz; değişiklikler PR ile gelir.

## 6. Alan sözlüğü

Aynı kavram için herkesin aynı ismi kullanması amacıyla:

| Türkçe | Kodda |
| --- | --- |
| Talep / Proje | `Project` |
| Talep Sahibi | `Requester` |
| Aşama | `ProcessStage` |
| Adım | `ProcessStep` |
| Adımda yapılan işlem | `StepRecord` |
| Seviye | `Level` |
| Doküman durumu | `DocumentStatus` |
| Gereksinim | `Requirement` |
| Risk | `Risk` (`Probability`, `Impact`, `Score`) |
| Açık konu | `OpenIssue` |
| Toplantı tutanağı | `Meeting` |
| Bildirim | `Notification` |
| Denetim kaydı | `AuditLog` |

Yeni bir kavram gerektiğinde önce bu tabloya eklenir.
