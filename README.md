# 📻 Radio Volna

![Wersja](https://img.shields.io/badge/wersja-1.6.4-blue)
![Platforma](https://img.shields.io/badge/Platforma-Android-brightgreen?logo=android&logoColor=white)
![Framework](https://img.shields.io/badge/Framework-.NET%20MAUI%208.0-purple?logo=dotnet&logoColor=white)
![Język](https://img.shields.io/badge/Język-C%23-239120?logo=csharp&logoColor=white)

**Nowoczesna i lekka aplikacja do słuchania radia internetowego zbudowana w technologii .NET MAUI.**

Radio Volna to autorski projekt odtwarzacza radiowego na system Android, zaprojektowany z myślą o stabilności połączenia i wysokiej jakości dźwięku. Aplikacja inteligentnie dobiera silnik odtwarzania w zależności od formatu strumienia stacji.

---

## ✨ Kluczowe Funkcje
* **Hybrydowy Silnik Audio:** Wykorzystuje zaawansowany **ExoPlayer (Media3)** dla strumieni HLS (m3u8) oraz natywny Android **MediaPlayer** dla standardowych linków audio.
* **Odtwarzanie w tle:** Pełne wsparcie dla usług systemowych (Foreground Service), co pozwala na słuchanie radia przy wyłączonym ekranie.
* **Inteligentne Zarządzanie Dźwiękiem:**
    * Automatyczna pauza po odłączeniu słuchawek lub głośnika Bluetooth (Noisy Audio).
    * Wsparcie dla **Audio Focus** – aplikacja wycisza się podczas nadchodzących połączeń i komunikatów nawigacji.
* **System Aktualizacji:** Automatyczne sprawdzanie dostępności nowej wersji aplikacji bezpośrednio z repozytorium GitHub.
* **Wielojęzyczność:** Pełne wsparcie dla lokalizacji (Resource Files), aktualnie dostępne w języku polskim i rosyjskim.

---

## 🛠️ Technologie i Narzędzia
* **.NET MAUI 8.0** – wieloplatformowy framework od Microsoft.
* **Android Native API** – bezpośrednie wykorzystanie usług systemowych Androida dla zapewnienia stabilności.
* **GitHub API** – mechanizm sprawdzania wersji i dostarczania aktualizacji.
* **C# / XAML** – czysty i czytelny kod źródłowy zgodny z wzorcem MVVM.

---

## 🚀 Jak uruchomić projekt?

### Wymagania
* Visual Studio 2022 (z zainstalowanym obciążeniem `.NET MAUI`).
* Android SDK (API Level 26 lub nowsze).

### Instalacja
1.  Sklonuj repozytorium:
    ```bash
    git clone [https://github.com/FNKM625/RadioVolna.git](https://github.com/FNKM625/RadioVolna.git)
    ```
2.  Otwórz plik `RadioVolna.sln` w Visual Studio.
3.  Zbuduj rozwiązanie (`Build Solution`), aby pobrać niezbędne pakiety NuGet.
4.  Podłącz telefon lub uruchom emulator i kliknij **F5**.

*Wskazówka: Do budowania gotowych plików APK możesz wykorzystać dołączony skrypt `buildApp.bat`.*

---

## 📝 Licencja i Prawa Autorskie
Ten projekt został udostępniony publicznie głównie w celach **edukacyjnych** oraz jako element **portfolio**.

**Wszelkie prawa zastrzeżone.**
* Kopiowanie, redystrybucja oraz komercyjne wykorzystanie kodu źródłowego lub grafiki bez wyraźnej zgody autora jest zabronione.
* Kod służy jako demonstracja umiejętności programistycznych w technologii .NET MAUI.

---

**Autor:** [FNKM625](https://github.com/FNKM625)  
**Status Projektu:** Rozwijany / Aktywny