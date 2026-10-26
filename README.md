# 🏛️ Game Of Temple

**Game Of Temple**, Unity ile geliştirilen, 2D izometrik olmayan yukarıdan görünümlü (Top-Down) bir macera oyunudur. Oyuncunun keşif ve etkileşim deneyimini; otonom yol bulma sistemleri, yapay zekâ destekli yoldaş kontrolü ve yerel Büyük Dil Modeli (LLM) ile çalışan NPC diyaloglarıyla bir araya getirir.

Projenin temel amacı, geleneksel oyun mekaniklerini modern yapay zekâ teknolojileriyle birleştirerek daha dinamik ve etkileşimli bir oyun deneyimi oluşturmaktır.

## 🎮 Proje Hakkında

Oyuncular, oyun dünyasındaki nesnelerle etkileşime girebilir, çevresel bulmacaları çözebilir ve yapay zekâ destekli karakterlerle iletişim kurabilir. Otonom yoldaş sistemi ise belirlenen hedeflere bağımsız olarak hareket edebilecek şekilde tasarlanmaktadır.

**Öne çıkan özellikler:**

- 🧭 **Otonom Yol Bulma:** Yapay zekâ destekli yoldaşın hedef koordinatlarına ulaşması.
- 💬 **LLM Tabanlı NPC Diyalogları:** Ollama üzerinden yerel dil modeliyle iletişim kurulması.
- 🗿 **Çevresel Etkileşimler:** Taş kapılar, sunaklar ve tabletler gibi oyun nesneleriyle etkileşim.
- ⚙️ **Modüler Mimari:** Bağımsız geliştirilebilen, arayüzler üzerinden haberleşen sistemler.
- 🔄 **Asenkron İşlemler:** Oyun akışını gereksiz yere bloke etmeden diyalog ve arayüz işlemlerinin yürütülmesi.

## 🏗️ Teknik Mimari

Proje, **SOLID prensipleri** doğrultusunda, sorumlulukların ayrıldığı ve bileşenler arasındaki bağımlılıkların azaltıldığı modüler bir mimari hedeflemektedir.

### Temel Katmanlar

| Katman                        | Sorumluluk                                                                                     |
| ----------------------------- | ---------------------------------------------------------------------------------------------- |
| **Oyun ve Arayüz Katmanı**    | Oyuncu girdileri, fizik tabanlı hareket, çevresel etkileşimler ve kullanıcı arayüzü.           |
| **Yapay Zekâ ve LLM Katmanı** | Otonom ajan navigasyonu, yoldaş komutları, yerel LLM iletişimi ve JSON yanıtlarının işlenmesi. |

### Arayüz Sözleşmeleri (Interface Contracts)

Sistemler arasındaki iletişimi standartlaştırmak ve bileşenleri birbirinden bağımsız tutmak için C# arayüzleri kullanılmaktadır.

- **`IInteractable`** — Oyun dünyasındaki etkileşime açık nesneler için ortak bir sözleşme sunar. Sunaklar, tabletler ve NPC'ler bu yapıyı kullanabilir.
- **`ICompanionCommand`** — Otonom yoldaşa hedef koordinatları ve hareket komutları iletilmesini sağlar.
- **`IDialogueService`** — Yerel dil modeliyle asenkron iletişimi yönetir ve `Task<NPCResponseDTO>` üzerinden yapılandırılmış NPC yanıtları sağlar.

### Kullanılan Teknolojiler

- **Unity** — Oyun motoru ve geliştirme ortamı.
- **C#** — Oyun mekanikleri ve sistem mimarisi.
- **Ollama** — Yerel dil modeli çalıştırma ve NPC diyalog altyapısı.
- **Unity AI Navigation / NavMesh** — Ajan navigasyonu için planlanan altyapı.
- **Unity ML-Agents** — Otonom ajan davranışları için değerlendirilmesi planlanan teknoloji.

> Not: NavMesh ve ML-Agents entegrasyonlarının tamamlanma durumu, projenin mevcut geliştirme aşamasına bağlıdır.

## 🗺️ Geliştirme Yol Haritası

Proje, aşağıdaki aşamalar doğrultusunda geliştirilmektedir.

- [ ] **Aşama 1 — Temel Mimari:** Arayüzlerin, veri aktarım nesnelerinin (DTO) ve temel sistemlerin oluşturulması.
- [ ] **Aşama 2 — Oynanış Mekanikleri:** Oyuncu hareketi, fizik tabanlı etkileşimler, taş kapılar ve sunak mekanikleri.
- [ ] **Aşama 3 — Diyalog ve Arayüz:** Asenkron UI akışları, Ollama entegrasyonu ve JSON yanıtlarının ayrıştırılması.
- [ ] **Aşama 4 — Yapay Zekâ ve Test:** Otonom yoldaş navigasyonu, ML-Agents entegrasyonu ve hata dayanıklılığı testleri.
- [ ] **Aşama 5 — Sonlandırma:** Son testler, dağıtıma hazırlık ve teknik dokümantasyon.

## 👥 Geliştirme Ekibi

| Ekip Üyesi                | Sorumluluk                                                                                                                                   |
| ------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| **Mehmet Han Güven**      | Proje mimarisi, çekirdek sistemler ve oynanış programlama. Projenin genel teknik yapısı ve temel sistemlerinin geliştirilmesinden sorumludur |
| **Muhammed Emin Sarıgül** | Yapay zekâ ve LLM çalışmaları. Yapay zekâ tabanlı sistemlerin geliştirilmesine ve yerel dil modeli entegrasyonuna katkı sağlamaktadır.       |

Her iki ekip üyesi de projenin geliştirme sürecine kendi sorumluluk alanları ve katkıları doğrultusunda dahil olmaktadır.



## 🎯 Projenin Vizyonu

Game Of Temple, oyun geliştirme süreçlerinde geleneksel yazılım mimarisi yaklaşımlarını yapay zekâ destekli etkileşimlerle birleştirmeyi hedefleyen bir projedir.

Modüler sistem tasarımı, otonom ajan davranışları ve yerel dil modeli entegrasyonu aracılığıyla sürdürülebilir, genişletilebilir ve etkileşim açısından zengin bir oyun altyapısı oluşturulması amaçlanmaktadır.

---

*Unity ile geliştiriliyor. Yapay zekâ ile şekilleniyor.*
