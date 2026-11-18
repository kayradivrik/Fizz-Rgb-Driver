# Fizz-Rgb-Driver (Türkçe Dökümantasyon)

<p align="center">
  <img src="docs/banner.svg" alt="Fizz-Rgb-Driver — Redragon Fizz K617 60% RGB Sürücüsü ve Makro Stüdyosu" width="100%">
</p>

> 🇬🇧 [Read in English (README.md)](README.md)

[![CI](https://img.shields.io/badge/build-passing-10b981?style=flat-square&logo=githubactions&logoColor=white)](#)
[![Release](https://img.shields.io/badge/release-v3.0.0-f97316?style=flat-square)](#)
[![License: MIT](https://img.shields.io/badge/License-MIT-eab308?style=flat-square)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078d4?style=flat-square)](#)
[![Memory](https://img.shields.io/badge/RAM-~0.9%E2%80%935%20MB%20Working%20Set-22c55e?style=flat-square)](#)
[![Hardware](https://img.shields.io/badge/hardware-Redragon%20Fizz%20K617-e53935?style=flat-square)](#)

**Redragon Fizz K617** (%60 mekanik klavye, USB `258a:0049`, Sinowealth SH68F90A MCU) için sıfırdan geliştirilmiş; arka planda yalnızca **~0.9 – 5 MB Working Set RAM** tüketen açık kaynaklı Windows RGB sürücüsü, 23 modlu gerçek zamanlı efekt motoru ve makro stüdyosu.

<p align="center">
  <img src="docs/app-preview.svg" alt="Fizz-Rgb-Driver Stüdyo Arayüzü" width="100%">
</p>

## Öne Çıkanlar

- **Native C# WPF Masaüstü Uygulaması + Tek Dosya Kurulum Sihirbazı (`Setup.exe`):** 61 tuşlu çift katmanlı (Double-Shot PBT) interaktif klavye sahnesi.
- **8 Dahili Çip (Onboard MCU) Efekti:** USB paket yakalamalarından tersine mühendislikle çıkarılan `rainbow`, `snake`, `waterfall`, `sine wave`, `star twinkle`, `rainbow blossom`, `wheel`, `static` modları doğrudan klavye hafızasına yazılır (uygulama kapalıyken bile çalışır).
- **382-Bayt `Col08` Doğrudan Tuş-Tuş RGB Akışı:** Önceden ayrılmış (*zero-allocation*) tampon bellek ve *Dirty-Flag* USB trafik koruması ile sabit modlarda %97 daha az arka plan yükü.
- **23+ Canlı Animasyon ve Ses/Tepki Modu:** Matrix Dijital Yağmur, 14-Bant Ses Ekolayzır (WASAPI), EKG Kalp Ritmi, Polar Radar, Termal Kor & Alev, Metaball Plazma (Lav Lambası), Cyberpunk Glitch, Kuzey Işıkları ve yazı hızınıza tepki veren *Ripple*, *Key Fade*, *Crosshair Lazer*, *Kıvılcım Patlaması* modları.
- **8 Hazır Espor & Tema Profili:** `Valorant FPS`, `Counter-Strike 2`, `League of Legends MOBA`, `Türk Bayrağı`, `Night City 2077`, `Outrun Synthwave`, `Horizon Termal`, `Sandbox Craft`.
- **Düşük Seviyeli Makro Stüdyosu (`WH_KEYBOARD_LL`):** Canlı tuş ve gecikme kaydı, *Tek Sefer*, *N Tekrar* ve *Aç/Kapa Döngü* modları.
- **Win32 Working Set RAM Temizleyici (`psapi.dll`):** `EmptyWorkingSet` çağrısıyla ~1 MB seviyesinde bellek kullanımı, Sistem Tepsisi (Tray) desteği ve boşta LED uyku zamanlayıcısı.

## Lisans

[MIT Lisansı](LICENSE) altında yayınlanmıştır.
