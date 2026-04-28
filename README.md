# 📻 Radio Volna

🌍 Available languages / Dostępne języki: [English](README.md) | [Polski](README.pl.md)

![Version](https://img.shields.io/badge/version-1.6.4-blue)
![Platform](https://img.shields.io/badge/Platform-Android-brightgreen?logo=android&logoColor=white)
![Framework](https://img.shields.io/badge/Framework-.NET%20MAUI%208.0-purple?logo=dotnet&logoColor=white)
![Language](https://img.shields.io/badge/Language-C%23-239120?logo=csharp&logoColor=white)

**A modern and lightweight internet radio application...**

## 📥 Download the App
You can download the latest compiled APK version directly to your Android device using the link below:

**[⬇️ Download Radio Volna (.apk)](https://drive.google.com/uc?export=download&id=1rV71ArRDhjOIr_YMqvDDZx8TSAqs7iEt)**

---

## ✨ Key Features
* **Hybrid Audio Engine:** Utilizes the advanced **ExoPlayer (Media3)** for HLS streams (m3u8) and the native Android **MediaPlayer** for standard audio links.
* **Background Playback:** Full support for system services (Foreground Service), allowing you to listen to the radio with the screen turned off.
* **Smart Audio Management:**
    * Automatic pause upon disconnecting headphones or Bluetooth speakers (Noisy Audio).
    * Support for **Audio Focus** – the app mutes itself during incoming calls and navigation prompts.
* **Update System:** Automatic checks for new application versions directly from the GitHub repository.
* **Multilingual:** Full support for localization (Resource Files), currently available in Polish and Russian.

---

## 🛠️ Technologies & Tools
* **.NET MAUI 8.0** – a cross-platform framework by Microsoft.
* **Android Native API** – direct use of Android system services to ensure stability.
* **GitHub API** – mechanism for version checking and delivering updates.
* **C# / XAML** – clean and readable source code compliant with the MVVM pattern.

---

## 🚀 How to run the project?

### Requirements
* Visual Studio 2022 (with the `.NET MAUI` workload installed).
* Android SDK (API Level 26 or newer).

### Installation
1.  Clone the repository:
    ```bash
    git clone [https://github.com/FNKM625/RadioVolna.git](https://github.com/FNKM625/RadioVolna.git)
    ```
2.  Open the `RadioVolna.sln` file in Visual Studio.
3.  Build the solution to restore the necessary NuGet packages.
4.  Connect your phone or start an emulator and press **F5**.

*Tip: To build ready-to-use APK files, you can use the included `buildApp.bat` script.*

---

## 📝 License and Copyright
This project is made publicly available primarily for **educational** purposes and as a **portfolio** piece.

**All rights reserved.**
* Copying, redistribution, and commercial use of the source code or graphics without explicit permission from the author are prohibited.
* The code serves as a demonstration of programming skills in .NET MAUI technology.

---

**Author:** [FNKM625](https://github.com/FNKM625)  
**Project Status:** In development / Active