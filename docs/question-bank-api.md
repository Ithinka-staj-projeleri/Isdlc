# Soru Bankası (Faz 4 · B1) — API Sözleşmesi

Şirketin dört formu aynı yapıdadır: bir **iş tipi** ve **alt kategori** seçilir, o seçime ait soru listesi çıkar, her soru
projede cevaplanır, formun sonucu cevaplardan hesaplanır. Bu doküman o ortak yapıyı (soru setleri, sorular, seçenekler,
cevaplar) ve uç noktalarını anlatır. Durum: **taslak, ekip onayı bekliyor.** Sorumlu: Baran.
Plan: [phase-4-plan.md](phase-4-plan.md) → B1. Veri modeli: [data-model.md](data-model.md) → "Soru bankası".

Bütün uç noktalar AGENTS.md'deki ortak kurallara uyar: `/api/v1/` öneki, enum'lar metin, tarihler UTC / ISO 8601,
hatalar `ProblemDetails`, JSON alanları camelCase, doğrulama hataları `errors.<alanAdı>` altında Türkçe.

## Kim kullanır

| Soru seti | Kaynak form | İşi kim yapar | Ekranı |
| --- | --- | --- | --- |
| `level` | Seviye Belirleme Şablonu (008) v3.0 | Baran (B2) | Seviye & Uygunluk sekmesi |
| `request-analysis` | Talep Anlama Soru Listesi (002) v2 | Dide (E3) | Talep Analizi sekmesi |
| `risk-analysis` | Risk Analiz Formu (014) v2.0 | Baran (B4) | Riskler sekmesi |
| `internal-approval` | Gereksinimler İç Onay Kontrol Listesi (012) v2.0 | Dide (D7) | Gereksinimler → İç onay |

Bu PR yalnızca **ortak yapıyı** kurar: tablolar, seed biçimi, soruları listeleme ve cevap kaydetme, ortak özet.
Her formun kendine özgü sonucu (seviye, uygunluk oranı, risk derecesi) ve ekranı, yukarıdaki işlerin kendi PR'larında yapılır;
sözleşmenin "Formlara özgü sonuçlar" bölümü onların tasarlayabilmesi için şimdiden yazıldı.

## Formlardan çıkan ortak yapı

Dört formun "Sorular" ve "Parametreler" sayfaları incelendi (`Arşiv` klasörü kullanılmadı):

| | Talep Analizi | Seviye | Risk Analizi | İç Onay |
| --- | --- | --- | --- | --- |
| Soru sayısı (iç / dış / ilişkili) | 63 / 69 / 63 | 24 / 26 / 24 | 45 / 46 / 45 | 39 / 39 / 39 |
| Zorunlu / duruma bağlı | var (ör. dış: 38 / 31) | yok | var (ör. dış: 31 / 15) | yok |
| Bölüm | Soru seviyesi: 1-Temel, 2-Kapsam, 3-Değerlendirme | Soru tipi: 1 Doküman seviyesi, 2 Araştırma seviyesi | — | — |
| Grup | Soru grubu ("İş İhtiyacı ve Problem") | — | Risk grubu + konu | Kontrol grubu |
| Seçenekler | Formun **ortak** dört durumu | **Her soruya özgü** 5 seçenek, kendi puanıyla | Formun ortak dört değerlendirmesi | Formun ortak dört durumu |
| Soruya eşlik eden metin | Beklenen bilgi / çıktı, kaynak | Kaynak | Olası risk tanımı, öngörülen aksiyon, kaynak | Kaynak |
| Cevapta tutulan | Yanıt metni, açık konu / eksik bilgi, durum | Seçilen seçenek | Değerlendirme, risk nedeni, olasılık (1–3), şiddet (1–3) | Durum, açıklama |

Ortak iskelet: **soru seti → (iş tipi, alt kategori) → bölüm → grup → soru → seçenek**; cevap ise **proje → doldurulan form → soru başına cevap.**

- **İş tipi:** formlarda şimdilik yalnızca *Yeni Yazılım Projesi* var. Kodda akışın anahtarıyla aynı tutulur
  (`new-software-project`); Dide'nin akış altyapısı (E1) akışlara verdiği anahtarı kullanır. Yeni bir iş tipi yeni soru satırı demektir, kod değişmez.
- **Alt kategori:** projenin talep kaynağıdır (`RequestSource`: `Internal` kurum içi, `External` dış / müşteri, `Affiliated` ilişkili şirket;
  `Affiliated` D1 ile eklenir). Formlardaki "TÜM ALT KATEGORİLER" satırları `null` alt kategoriyle tutulur ve her alt kategoride listelenir.

## Kavramlar

| Kavram | Kodda | Ne tutar |
| --- | --- | --- |
| Soru seti | `QuestionSet` | Form: kod (`level`), ad, kaynak doküman kodu ve sürümü, cevap biçimi |
| Soru | `Question` | Hangi iş tipi ve alt kategoride çıktığı, numara, sıra, bölüm, grup, konu, metin, zorunluluk, yardımcı metinler |
| Seçenek | `QuestionOption` | Bir soruya (seviye) **ya da** bütün sete (diğer üçü) ait seçenek: kod, etiket, puan, anlamı |
| Doldurulan form | `Questionnaire` | Bir projede bir soru setinin doldurulması: iş tipi, alt kategori, katılanlar, tarih, onaylayan |
| Cevap | `QuestionAnswer` | Bir sorunun cevabı: seçenek, metin, not, olasılık, şiddet |

**Seçeneğin anlamı** (`OptionMeaning`): hesap ve ekran seçeneğin etiketine değil anlamına bakar; etiketler formdan aynen alınır.

| Anlam | Talep Analizi | Risk Analizi | İç Onay |
| --- | --- | --- | --- |
| `Complete` | Yanıtlandı | Risk Yok | Uygun |
| `Partial` | Kısmen Yanıtlandı | — | Kısmen Uygun |
| `Open` | Açık Konu Var | Belirsiz (Bilgi Eksik) | — |
| `Negative` | — | Risk Var | Uygun Değil |
| `NotApplicable` | Uygulanabilir Değil | Uygulanabilir Değil | Uygulanamaz |

Seviye setinde seçenekler soruya özgüdür ve anlamı yoktur, yalnızca puanı vardır. Risk Analizi'nde "Risk Var" ve
"Belirsiz" **puanlanır** (`isScored`): bu cevaplarda olasılık ve şiddet girilir, satır risk kaydına aktarılabilir (B4).

## Ortak kurallar

- **Soru bankası ana veridir:** kullanıcı değiştirmez, seed'den yüklenir, denetim kaydına yazılmaz (aşağıda "Seed").
- **Doldurulan form ve cevaplar proje verisidir:** `IAudited` + `IProjectRecord`; her değişiklik projenin Kayıt Geçmişi'nde görünür.
- **Okuma:** giriş yapmış her kullanıcı. **Yazma:** giriş yapmış her aktif kullanıcı, **yalnızca aktif projede** (değilse 409). Proje kayıtlarıyla aynı.
- Bir projede her soru setinden **bir** doldurulan form vardır; ilk cevap kaydedilince oluşur. Silinmez; cevaplar değiştirilebilir ya da boşaltılabilir.
- **Alt kategori** form ilk oluşurken projenin talep kaynağından alınır. Sonradan değiştirilirse soru listesi değişir:
  yeni alt kategoride olmayan soruların cevapları silinmez, listede görünmez ve özete katılmaz (kaynak geri alınırsa geri gelir).
- **Zorunluluk:** `Required` (Zorunlu) ya da `Conditional` (Duruma Bağlı). Duruma bağlı soru, projeye uymuyorsa `NotApplicable` seçilerek kapatılır.
  Formun **tamamlanmış** sayılması için bütün zorunlu soruların bir seçeneği olmalıdır. Duruma bağlı sorular tamamlanmayı engellemez.
- Cevaplar **tek tek** kaydedilir (satır satır doldurulan Excel gibi); bütün formu birden göndermek gerekmez.
- **Eş zamanlı düzenleme:** aynı cevabı aynı anda iki kişi kaydederse son kaydeden kazanır.

## Uç noktalar

| İstek | Açıklama | Başarılı cevap |
| --- | --- | --- |
| `GET /question-sets` | Soru setleri (kod, ad, kaynak doküman, sürüm, cevap biçimi) | `200` `QuestionSetDto[]` |
| `GET /question-sets/{setCode}/questions?workType=&subcategory=` | Bir iş tipi ve alt kategorinin soruları, sırasıyla ve seçenekleriyle (form doldurulmadan önce görmek için) | `200` `QuestionDto[]` |
| `GET /projects/{projectId}/questionnaires/{setCode}` | Projedeki form: başlık bilgileri, sorular, cevaplar ve özet. Hiç cevap yoksa boş cevaplarla döner | `200` `QuestionnaireDto` |
| `PUT /projects/{projectId}/questionnaires/{setCode}` | Başlık bilgilerini kaydet (alt kategori, katılanlar, tarih, onaylayan) | `200` `QuestionnaireDto` |
| `PUT /projects/{projectId}/questionnaires/{setCode}/answers/{questionId}` | Bir sorunun cevabını kaydet ya da boşalt | `200` `QuestionnaireDto` |

Listeler **sayfalanmaz**: bir formda en fazla ~70 soru vardır ve ekran onları bölüm ve gruplara ayırarak tek sayfada gösterir.
Cevap kaydedince bütün form döner, çünkü özet (sayılar, sonuç) her cevapla değişir.

## DTO'lar

```jsonc
// QuestionSetDto
{ "code": "risk-analysis", "name": "Risk Analiz Formu", "sourceDocument": "ISDLC-Form-Analiz-014", "version": "2.0",
  "answerMode": "SharedOptions" }        // SharedOptions: seçenekler setin; QuestionOptions: her sorunun kendi seçenekleri (level)

// QuestionDto
{
  "id": 412,
  "code": "risk-analysis/new-software-project/all/001",   // seed'deki kalıcı anahtar (aşağıda "Seed")
  "no": 1,
  "section": null,                                         // talep analizinde "1-Temel", seviyede "1" / "2"
  "group": "Kapsam ve Gereksinim",
  "topic": "Kapsam Netliği",                               // yalnızca risk analizinde
  "text": "Talep kapsamı, beklenen çıktılar ve teslimatlar Proje Kapsam Dokümanı'nda yazılı ve talep sahibince teyit edilmiş mi?",
  "requirement": "Required",                               // Required | Conditional | null (formda zorunluluk yoksa)
  "expectedInfo": null,                                    // talep analizinde "Beklenen Bilgi / Çıktı"
  "riskDescription": "Kapsamın yazılı ve teyitli olmaması nedeniyle …",   // yalnızca risk analizinde
  "suggestedAction": "Kapsam ve teslimatlar Proje Kapsam Dokümanı'nda yazılmalı …", // yalnızca risk analizinde
  "reference": "SEI TBQ A.1.b Completeness …",
  "options": [ { "id": 3, "code": "risk-present", "label": "Risk Var", "points": null, "meaning": "Negative", "isScored": true } ]
}

// QuestionnaireDto
{
  "projectId": 3,
  "set": { /* QuestionSetDto */ },
  "workType": "new-software-project",
  "subcategory": "External",
  "participants": "Elif Yılmaz, Müşteri BT",
  "assessedOn": "2026-11-11",
  "approvedBy": null,                                     // iç onayda onaylayan (UserSummaryDto), diğerlerinde boş
  "approvedOn": null,
  "questions": [
    {
      "question": { /* QuestionDto */ },
      "answer": {                                          // cevaplanmadıysa null
        "optionId": 3,
        "text": null,                                      // talep analizinde paydaş yanıtı
        "note": "ERP servis dokümanı istendi, gelmedi.",   // açık konu / risk nedeni / açıklama
        "probability": 2,                                  // yalnızca puanlanan risk cevabında, 1–3
        "severity": 3,
        "updatedBy": { "id": 1, "fullName": "Elif Yılmaz" },
        "updatedAt": "2026-11-11T09:12:00Z"
      }
    }
  ],
  "summary": {
    "listed": 46, "answered": 12, "requiredUnanswered": 19, "isComplete": false,
    "byMeaning": { "Complete": 6, "Partial": 1, "Open": 2, "Negative": 3, "NotApplicable": 0 },
    "missingNote": 1                                       // not ister seçenekte not yazılmamış cevap
  },
  "result": null                                           // formlara özgü sonuç (aşağıda); bu PR'da hep null
}

// SaveQuestionnaireDto (PUT .../questionnaires/{setCode})
{ "subcategory": "External", "participants": "Elif Yılmaz, Müşteri BT", "assessedOn": "2026-11-11",
  "approvedByUserId": null, "approvedOn": null }

// SaveAnswerDto (PUT .../answers/{questionId}) — bütün alanlar null ise cevap boşaltılır
{ "optionId": 3, "text": null, "note": "ERP servis dokümanı istendi, gelmedi.", "probability": 2, "severity": 3 }
```

| Alan | Kural | Hata mesajı |
| --- | --- | --- |
| `optionId` | Soruya (ya da sorunun setine) ait bir seçenek olmalı | "Seçilen cevap bu soruya ait değil." |
| `text` | İsteğe bağlı, en fazla 4000 karakter | "Yanıt en fazla 4000 karakter olabilir." |
| `note` | İsteğe bağlı, en fazla 2000 karakter. Seçeneği not isteyen cevaplarda zorunlu değildir ama özette `missingNote` olarak sayılır (formdaki gibi uyarı, engel değil) | "Not en fazla 2000 karakter olabilir." |
| `probability`, `severity` | Yalnızca puanlanan seçenekte, 1–3; puanlanmayan seçenekte gönderilirse yok sayılmaz, 400 döner | "Olasılık 1 ile 3 arasında olmalıdır.", "Olasılık ve şiddet yalnızca puanlanan cevaplarda girilir." |
| `questionId` | Formun iş tipi ve alt kategorisinde listelenen bir soru olmalı | "Soru bu formda yer almıyor." (404) |
| `subcategory` | `Internal`, `External`, `Affiliated` | "Alt kategori seçilmelidir." |
| `approvedByUserId` | İsteğe bağlı; aktif bir kullanıcı | "Seçilen onaylayan aktif bir kullanıcı değil." |

## Formlara özgü sonuçlar

`QuestionnaireDto.result` alanını her formun sahibi kendi PR'ında doldurur. Hesaplar saf kural sınıflarında yazılır ve her
kombinasyonda veritabanısız test edilir (AGENTS.md Bölüm 9). Eşikler seed'den okunur, koda yazılmaz.

| Set | Sonuç | Kural (formdan) | İş |
| --- | --- | --- | --- |
| `level` | Doküman seviyesi ve araştırma süresi | Bölüm 1 puanlarının toplamı: 1–20 Seviye 1, 21–40 Seviye 2, 41–60 Seviye 3. Bölüm 2: 1–10 → 1 gün, 11–20 → 2, 21–30 → 5, 31–40 → 10, 41–50 → 20, 51–60 → 30 gün. Seçilmemiş soru varsa sonuç yok | B2 |
| `request-analysis` | Açık konular | `Open` ve `Partial` cevaplar; tek tıkla Açık Konular'a aktarılır | E3 |
| `risk-analysis` | Risk özeti | Puanlanan her cevapta derece = olasılık × şiddet (1–9). Seviye: 1 Hafif (C), 2 Tolere Edilebilir (C), 3–4 Orta Dereceli (B), 6 Yüksek Risk (A), 9 Tolere Edilemez (A). Özet: seviyelere göre sayı, en yüksek derece, ortalama derece, puanı eksik satır. Ölçek Risks modülüyle ortaktır (B3) | B4 |
| `internal-approval` | Uygunluk oranı | Uygun / (Listelenen − Uygulanamaz). `Partial`, `Negative`, `NotApplicable` cevaplarda açıklama beklenir | D7 |

## Seed

- Her soru seti bir JSON dosyasıdır: `Isdlc.Infrastructure/Seed/question-sets/{setCode}.json`. Formun Excel'inden bir kez
  çıkarılır; **şablon dosyası repoya konmaz**, yalnızca soru satırları, seçenekler ve eşikler alınır.
- Her sorunun **kalıcı bir anahtarı** vardır: `{set}/{iş tipi}/{alt kategori ya da all}/{no}` (`risk-analysis/new-software-project/all/001`).
  Cevaplar soruya bu anahtar üzerinden bağlıdır; analistler formu güncelleyince dosya değişir ve uygulama açılırken eşitlenir:
  - metin, grup, zorunluluk, yardımcı metin ve seçenek etiketi değişikliği → yerinde güncellenir, cevaplar korunur;
  - yeni soru → eklenir;
  - dosyadan çıkan soru → silinmez, **pasif** olur (yeni formda listelenmez; eski cevabı Kayıt Geçmişi'nde kalır).
- Dosyalar işleme alınmadan önce doğrulanır: tekrar eden anahtar, olmayan seçenek, puansız seviye seçeneği, eşiklerde boşluk ya da çakışma → uygulama açılmaz ve hata loga yazılır.

```jsonc
// question-sets/level.json (kısaltılmış)
{
  "code": "level", "name": "Seviye Belirleme Şablonu", "sourceDocument": "ISDLC-Doküman-Analiz-008", "version": "3.0",
  "answerMode": "QuestionOptions",
  "bands": {                                    // formlara özgü eşikler; hesabı ilgili işin kural sınıfı yapar
    "1": [ { "min": 1, "max": 20, "result": "1" }, { "min": 21, "max": 40, "result": "2" }, { "min": 41, "max": 60, "result": "3" } ],
    "2": [ { "min": 1, "max": 10, "result": "1d" }, { "min": 11, "max": 20, "result": "2d" } /* … */ ]
  },
  "questions": [
    { "workType": "new-software-project", "subcategory": "Internal", "no": 1, "section": "1",
      "text": "Talep analizi sonucunda belirlenen temel iş senaryosu ve modül sayısı nedir?",
      "options": [ { "label": "Tek iş senaryosu / tek fonksiyon", "points": 1 }, { "label": "2–5 iş senaryosu, tek modül", "points": 2 } /* … */ ],
      "reference": "BABOK v3 §3.1 …" }
  ]
}
```

## Hata durumları

| Durum | Kod | Örnek `detail` |
| --- | --- | --- |
| Proje veya soru seti bulunamadı | `404` | "Soru seti bulunamadı." |
| Soru bu formda yok | `404` | "Soru bu formda yer almıyor." |
| Seçenek bu soruya ait değil, aralık dışı puan | `400` | "Seçilen cevap bu soruya ait değil." |
| Proje aktif değil | `409` | "Proje aktif değil; kayıt eklenemez veya değiştirilemez." |
| İki kişi formu aynı anda ilk kez kaydetti | `409` | "Form bu arada başka biri tarafından kaydedildi. Sayfayı yenileyin." |

## Kod yerleşimi

| Katman | Dosyalar |
| --- | --- |
| `Isdlc.Domain/QuestionBank/` | `QuestionSet`, `Question`, `QuestionOption`, `Questionnaire`, `QuestionAnswer`, enum'lar |
| `Isdlc.Application/QuestionBank/` | `IQuestionBankService` (sorular), `IQuestionnaireService` (proje formları), DTO'lar, `QuestionnaireRules` (listeleme, tamamlanma, özet; saf) |
| `Isdlc.Infrastructure/QuestionBank/` | EF yapılandırmaları, `QuestionSetSeeder` (JSON doğrulama ve eşitleme) |
| `Isdlc.Infrastructure/Seed/question-sets/` | `level.json`, `request-analysis.json`, `risk-analysis.json`, `internal-approval.json` |
| `Isdlc.Api/Controllers/` | `QuestionSetsController`, `QuestionnairesController` |
| `frontend/src/app/shared/question-bank/` | Ortak bileşen: bölüm ve gruplara ayrılmış soru listesi, satır satır cevap, özet şeridi. Dört sekme bunu kullanır |

## Uygulama sırası (her biri ayrı PR)

1. Bu doküman (ekip onayı)
2. Entity'ler, migration, seeder ve dört setin JSON dosyaları (Excel'den çıkarma betiği repoya konmaz; dosyalar elle doğrulanır)
3. Servisler ve API; `QuestionnaireRules` testleri
4. Ortak ekran bileşeni (`shared/question-bank`); ilk kullanan B2 olur

## Ekip onayı bekleyen kararlar

1. **Dört form tek yapıda** tutulur; formların farkları seçenek anlamı (`OptionMeaning`), cevap biçimi (`answerMode`) ve formlara özgü sonuç hesabıyla karşılanır.
2. **Alt kategori = talep kaynağı** (`RequestSource`). D1'de `Affiliated` (İlişkili şirket) eklenince üç alt kategori de çalışır. *(Dide)*
3. **İş tipi = akışın anahtarı** (`new-software-project`). E1 akış anahtarlarını bu biçimde verirse soru setleri akışla doğrudan eşleşir. *(Dide)*
4. **Ek geliştirme akışının soru seti yok:** formlarda yalnızca Yeni Yazılım Projesi var. Analistlere sorulana kadar ek geliştirme kayıtları
   ana projenin iş tipinin (`new-software-project`) sorularını kullanır. *(phase-4-plan.md → Açık konular'a eklenmeli)*
5. **Cevaplar tek tek kaydedilir** ve her kayıt bütün formu (özetiyle) döndürür.
6. **Dosyadan çıkan soru silinmez, pasif olur;** cevabı ve geçmişi korunur.
7. **Not ve açıklama uyarıdır, engel değildir** (formlarda da öyle): eksikleri özet sayar, kaydı durdurmaz.
8. **Müşteri Uygunluk Anketi (006)** Word formu olduğu için bu dört sete dahil edilmedi; yapısı (10 soru, puanlı seçenekler) `level` ile aynı olduğu
   için B2'de beşinci set olarak eklenecek.
