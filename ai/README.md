# ai/ — Yapay zekâ ajanları

ISDLC'nin yapay zekâ ajanları (Python). İki ajan da uygulamada: yeni tutanak formundaki "Yapay zekâ ile karar ve
aksiyonları çıkar" butonu ve Riskler sekmesindeki "Yapay zekâ ile risk öner" / risk formundaki "Yapay zekâ ile puan ve
aksiyon öner" butonları, backend üzerinden bu klasördeki HTTP servisini çağırır. Komut satırı deneme ve eval için duruyor. Tasarım ve yol haritası: [docs/ai-vision.md](../docs/ai-vision.md). Sorumlu: Erva.

Şu an iki ajan var. Her ajan birkaç **görevden** oluşur; her görev kendi prompt dosyası ve cevap şemasıyla
ayrı bir model çağrısıdır.

| Ajan | Görevler | Kayıt |
| --- | --- | --- |
| **Toplantı ajanı** | 1. Açık konu önerileri (aksiyon, karar, soru, bağımlılık, varsayım, değişiklik talebi) <br> 2. Tutanak taslağı (tür, katılımcılar, gündem) | Uygulamada ikisi birlikte tutanak formunu doldurur (tür, katılımcılar, gündem yalnızca boşsa). Komut satırında açık konular onaylanınca API'ye yazılır |
| **Risk ajanı** | 1. Riskleri bulma <br> 2. Olasılık ve etki puanlama (1-5) <br> 3. Önleyici aksiyon | Risks API'si gelene kadar yalnızca çıktı dosyasında |

Ajanlara görevlerin nasıl bölüştürüldüğü: [docs/ai-vision.md → Ajan ve görev](../docs/ai-vision.md#ajan-ve-görev).

## Bu klasörün kuralları

1. **Ajan veritabanına dokunmaz.** Okuma ve yazma yalnızca ISDLC API'si (`/api/v1/...`) üzerinden yapılır.
   Böylece doğrulama, yetki, kod numarası ve denetim kaydı kuralları ajan için de geçerli olur.
2. **Ajan kayıt oluşturmaz, öneri üretir.** Kaydı oluşturma kararı her zaman bir kişidedir.
3. **Yalnızca sahte veriyle çalışılır.** Gerçek müşteri verisi, şirket modele veri gönderme kararını verene kadar kullanılmaz.
4. **Kişisel veriler modele gönderilmeden önce gizlenir** (`masking.py`: e-posta, telefon, IBAN, TCKN).
   Kişi adları henüz gizlenmiyor; bu, sonraki bir adım.
5. **API anahtarı koda yazılmaz.** `ai/.env` dosyasında durur ve git'e girmez.
6. Model sağlayıcısı `llm.py`'deki `LlmClient` arayüzünün arkasındadır. Sağlayıcı değişirse yalnızca yeni bir sınıf eklenir.
7. Prompt'lar `isdlc_ai/prompts/<ajan>/` altında görev adı ve sürüm numarasıyla tutulur (`meeting/open_issues.v1.md`).
   Prompt değişirse yeni sürüm dosyası açılır ve değerlendirme (eval) yeniden çalıştırılır.

## Kurulum

Python 3.11 veya üstü gerekir. Komutlar `ai/` klasöründe çalıştırılır.

```bash
python -m venv .venv
```

```bash
.venv/Scripts/python -m pip install -r requirements-dev.txt
```

macOS / Linux'ta `.venv/Scripts/python` yerine `.venv/bin/python` yazılır.

`.env.example` dosyasını `.env` adıyla kopyalayıp `GEMINI_API_KEY` değerini girin.

Google eski modelleri zamanla kapatır. "model ... is no longer available" hatası alırsanız anahtarınızın
kullanabildiği modelleri listeleyip `GEMINI_MODEL` değerini değiştirin:

```bash
.venv/Scripts/python -m isdlc_ai models
```

## Testler

Testler gerçek modeli çağırmaz (`FakeLlmClient`); anahtar ve internet gerekmez.

```bash
.venv/Scripts/python -m pytest
```

## Uygulamayla birlikte çalıştırma (HTTP servisi)

Backend, servisi `appsettings.Development.json`'daki `Agents:ServiceUrl` (`http://localhost:8000`) adresinden,
`Agents:ServiceKey` anahtarıyla çağırır. Aynı anahtar `ai/.env` içinde `AGENT_SERVICE_KEY` olarak durur
(`.env.example`'daki geliştirme değeri). Servis çalışmıyorsa uygulamadaki yapay zekâ butonu görünmez.

Lokalde (Python kurulumu yukarıda):

```bash
.venv/Scripts/python -m uvicorn isdlc_ai.server:app --port 8000
```

Ya da Docker ile (kök klasörde):

```bash
docker compose --profile ai up -d --build
```

| Uç nokta | Ne yapar |
| --- | --- |
| `GET /health` | Servis ayakta mı, model ayarlı mı (`modelConfigured`) |
| `POST /v1/meeting/suggestions` | Notlardan açık konu önerileri (`X-Agent-Key` gerekir). Sözleşme: [open-issues-meetings-api.md](../docs/open-issues-meetings-api.md) → "Yapay zekâ önerileri" |
| `POST /v1/risk/suggestions` | Metinden riskler: bul, puanla, önleyici aksiyon öner (üç model çağrısı). Sözleşme: [risks-api.md](../docs/risks-api.md) → "Yapay zekâ önerileri" |
| `POST /v1/risk/assessment` | Kişinin yazdığı tek risk için olasılık, etki ve önleyici aksiyon (iki model çağrısı) |

Servis yalnızca backend tarafından çağrılır; ISDLC API'sine ve veritabanına bu akışta dokunmaz.
Model hata verirse Türkçe bir mesajla `503` döner; backend bu mesajı kullanıcıya iletir.

## Toplantı ajanını komut satırından çalıştırma

Önce backend'i lokalde çalıştırın (kök README). Proje Id'lerini Swagger'dan görebilirsiniz.

Yalnızca önerileri görmek için (hiçbir şey kaydedilmez):

```bash
.venv/Scripts/python -m isdlc_ai meeting --project-id 1 --meeting-date 2026-11-04 --notes notlar.txt
```

Önerileri tek tek onaylayıp açık konu olarak kaydetmek için `--apply` eklenir:

```bash
.venv/Scripts/python -m isdlc_ai meeting --project-id 1 --meeting-date 2026-11-04 --notes notlar.txt --apply --default-owner "Elif Yılmaz"
```

- Sorumlusu notlarda yazmayan öneri için sorumlu sorulur (`--default-owner` varsayılan değeri verir).
- `Decision` (karar) türündeki öneriler, tutanak kuralıyla aynı şekilde kapalı (`Closed`) olarak kaydedilir.
- Her çalıştırmanın sonucu `ai/output/` klasörüne JSON olarak yazılır (git'e girmez).

Backend çalışmıyorsa `--project-id` yerine `--project-code ISDLC-2026-014` verilerek yalnızca öneriler görülebilir.

Tutanak taslağını da istemek için `--minutes` eklenir. Aynı notlar ikinci bir prompt'la, ayrı bir çağrıda modele gönderilir.
Çıktı dosyasındaki `createMeetingDtoPreview`, Meetings API'sine gidecek isteğin önizlemesidir; kararlar ve aksiyonlar ilk görevin önerilerinden alınır.

```bash
.venv/Scripts/python -m isdlc_ai meeting --project-code ISDLC-2026-014 --meeting-date 2026-11-04 --notes notlar.txt --minutes
```

## Risk ajanını çalıştırma

Bir metindeki riskleri bulur, puanlar ve önleyici aksiyon önerir (üç görev, üç model çağrısı):

```bash
.venv/Scripts/python -m isdlc_ai risks --project-code ISDLC-2026-014 --notes notlar.txt
```

Metin toplantı notu değilse ne olduğu yazılır: `--source-label "Analiz dokümanı"`.

Riskleri kendiniz yazdıysanız (her satıra bir risk), bulma görevi atlanır; yalnızca puanlama ve aksiyon çalışır:

```bash
.venv/Scripts/python -m isdlc_ai risks --project-code ISDLC-2026-014 --risk-list riskler.txt
```

Skor (olasılık × etki) modelden istenmez, kodda hesaplanır. Riskler skora göre sıralanarak gösterilir.

## Değerlendirme (eval)

`evals/<ajan>/cases/` altında sahte metinler ve her birinde beklenen kayıtlar bulunur.
Betikler gerçek modeli çağırır, bu yüzden token harcar:

```bash
.venv/Scripts/python evals/meeting/run_eval.py
```

```bash
.venv/Scripts/python evals/risk/run_eval.py
```

Çıktı, her vaka için kaç beklenen kaydın bulunduğunu, kaçırılanları ve fazla önerileri gösterir
(toplantıda yanlış terminleri, riskte beklenenden düşük skorları da).
Prompt veya model değiştirildiğinde sonuçlar öncekiyle karşılaştırılır. Yeni vaka eklemek için klasöre bir JSON dosyası eklemek yeterlidir.

## Klasör yapısı

```
ai/
├─ isdlc_ai/
│  ├─ __main__.py            # komut satırı: python -m isdlc_ai meeting ...
│  ├─ server.py              # HTTP servisi (backend çağırır): uvicorn isdlc_ai.server:app
│  ├─ config.py              # ai/.env ayarları
│  ├─ llm.py                 # LlmClient arayüzü, GeminiClient, FakeLlmClient
│  ├─ masking.py             # kişisel veri gizleme
│  ├─ isdlc_api.py           # ISDLC API istemcisi
│  ├─ schemas.py             # API ile ortak yapılar (OpenIssueSuggestion → CreateOpenIssueDto, MeetingMinutesSuggestion, RiskSuggestion)
│  ├─ agents/
│  │  ├─ base.py             # bütün görevlerin ortak kısmı: maskeleme, model çağrısı, tekrar deneme, dayanak kontrolü
│  │  ├─ meeting_agent.py    # toplantı ajanı: suggest_open_issues, draft_minutes
│  │  └─ risk_agent.py       # risk ajanı: identify, assess, mitigate (run = üçü sırayla)
│  └─ prompts/
│     ├─ meeting/            # open_issues.v1.md, minutes.v1.md
│     └─ risk/               # identify.v2.md (kategori eklendi; v1 iz için duruyor), assess.v1.md, mitigate.v2.md (uydurma yer tutucu yasağı)
├─ tests/
└─ evals/
   ├─ meeting/               # örnek vakalar ve değerlendirme betiği
   └─ risk/
```

## Yeni görev ekleme

Örnek: toplantı ajanına "sonraki toplantının gündemini öner" görevi.

1. **Prompt:** `isdlc_ai/prompts/meeting/next_agenda.v1.md`. Görevi, kuralları ve uydurmama kuralını yazın.
2. **Cevap şeması ve görev:** ajan dosyasında modelden istenen şemayı ve görevi tanımlayın:
   `NEXT_AGENDA = Task("meeting.next_agenda", "meeting/next_agenda.v1", NextAgendaLlmOutput)`.
3. **Metot:** ajana `suggest_next_agenda(meeting)` ekleyin. Metot mesajı kurar, `run_task(...)` çağırır,
   cevabı doğrular ve göreve özel uyarıları ekler. Maskeleme ve tekrar deneme `run_task`'ta hazırdır.
4. **Kayıt şekli:** önerinin API'ye gidecek hali `schemas.py`'de, ilgili `Create...Dto` ile aynı sınırlarla tanımlanır.
5. **Test ve eval:** `tests/` altında `FakeLlmClient` ile testler, `evals/<ajan>/cases/` altında örnek vakalar.

Görev başka türde bir kayıt üretiyorsa (ör. gereksinim) mevcut ajana değil, o kaydın ajanına eklenir.

## Henüz yapılmayanlar

- Uygulamada öneriler tutanak formunda inceleniyor; kabul / ret oranlarını ölçmek için `AgentSuggestion` tablosu ve öneri paneli sonra gelecek (bkz. ai-vision.md).
- Komut satırında sorumlu her zaman `ownerName` olarak gönderiliyor. Uygulamada backend, adı aktif bir kullanıcıyla eşleşen sorumluyu `ownerUserId` yapıyor.
