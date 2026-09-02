<div align="center">

# IT-Enterprise Resource Calculator — Source Code

[![Deployed to](https://img.shields.io/badge/Deployed_to-Calculator--servers-blue)](https://github.com/ajjs1ajjs/Calculator-servers)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> **Це репозиторій з вихідним кодом IT-Enterprise Resource Calculator.**
> Готовий продукт деплоїться в: **https://github.com/ajjs1ajjs/Calculator-servers**
> Офіційний сайт: **https://ajjs1ajjs.github.io/Calculator-servers/**

<img src="docs/banner.svg?v=4" width="100%" alt="IT-Enterprise Resource Calculator">

# 🧮 IT-Enterprise Resource Calculator

**Калькулятор ресурсів IT-інфраструктури на основі матриці сайзингу**
[![Release](https://img.shields.io/github/v/release/ajjs1ajjs/Calculator-servers?label=release&color=7B2FFF)](https://github.com/ajjs1ajjs/Calculator-servers/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/ajjs1ajjs/Calculator-servers/total?label=downloads&color=00E5FF)](https://github.com/ajjs1ajjs/Calculator-servers/releases)
[![CI](https://img.shields.io/github/actions/workflow/status/ajjs1ajjs/Calculator-servers/ci.yml?label=CI)](https://github.com/ajjs1ajjs/Calculator-servers/actions)
[![Tests](https://img.shields.io/badge/tests-131%20passing-00C853)](https://github.com/ajjs1ajjs/Calculator-servers/actions)
[![Platform](https://img.shields.io/badge/platform-Windows-00E5FF)]()
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)]()
[![License](https://img.shields.io/badge/license-MIT-26A69A)](LICENSE)

**WPF + Avalonia · MVVM · .NET 10** — десктоп-застосунок для автоматизованого розрахунку ресурсів IT-інфраструктури.
Працює на **Windows 10/11** (WPF + Avalonia).

<a href="https://github.com/ajjs1ajjs/Calculator-servers/releases/latest"><img src="https://img.shields.io/badge/Download-latest-00A0C6"></a>

</div>

---

## ✨ Можливості

| | |
|---|---|
| 🧮 **3 кроки** | «Матриця» → «Параметри розрахунку» → «Результати». |
| 🛠️ **Редагування матриці** | Усі діапазони (SQL/Postgres/Oracle/App/Web), компоненти та формули модулів, вузли інфраструктури — змінюються в UI та зберігаються в `matrix.json` без передеплою. |
| 🔐 **Захист матриці** | Зміна чутливих даних (діапазони, формули, вузли) потребує пароля; при забутому паролі — контакти розробника для відновлення доступу. |
| ⚙️ **Налаштування рушія** | Константи розрахунку (SmartID, IOPS-профілі, ліміти SQL, pagefile) редагуються через матрицю. |
| 🖥️ **3 режими розгортання** | Kubernetes, Windows та Hybrid (app/web — Windows-VM, ForceBPM та інші — K8s, БД — Windows). |
| 🌍 **4 середовища** | PROD (завжди), DEV, TEST та PreProd — з порівняльною таблицею. |
| 🗄️ **3 типи СУБД** | MS SQL Server, PostgreSQL, Oracle 19c. |
| ⚡ **Профіль навантаження** | Єдиний продуктивний профіль (Performance). |
| 💾 **Вимоги до дисків** | Окрема розкладка для БД (OS / Logs+TempDB / Data / Content) та файл підкачки для app/web-вузлів. |
| 🧩 **Опціональні вузли** | Сервер звітів, SQL Secondary (failover) та HAProxy з режимом High Availability (2 вузли, keepalived/VRRP, спільний VIP). |
| 📤 **Експорт звіту** | Excel (.xlsx, для тендерів), XML та HTML. |
| 🕘 **Історія розрахунків** | Останні 20 розрахунків зберігаються локально. |
| 🌐 **Локалізація** | Українська та англійська мови. |

---

## 📸 Інтерфейс

<div align="center">

<img src="docs/screenshots/main.png" width="700" alt="IT-Enterprise Resource Calculator">

</div>

---

## 🚀 Швидкий старт

### З вихідного коду

```bash
# Windows — WPF-версія (основна)
dotnet run --project ResourceCalculator/ResourceCalculator.csproj

# Windows — Avalonia-версія
dotnet run --project ResourceCalculator.Avalonia/ResourceCalculator.Avalonia.csproj
```

### Збірка та тести

```bash
dotnet build ResourceCalculator.slnx -c Release
dotnet test  ResourceCalculator.slnx -c Release
```

> Потрібен [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) (версія зафіксована у [`global.json`](global.json)).

### Публікація

```bash
# Windows (WPF, win-x64, self-contained, ~50 МБ, один exe)
dotnet publish ResourceCalculator/ResourceCalculator.csproj -c Release --output publish
```

---

## 📦 Розповсюдження

| Платформа | Артефакт | Файл | Примітка |
|---|---|---|---|
| **Windows** | Портативний `.exe` (WPF) | `publish/ITE.ResourceCalculator.exe` | Self-contained, один файл, ~50 МБ. Перевірка оновлень через GitHub Release. |
| **Windows** | MSI-інсталятор | `publish/ITE.ResourceCalculator.msi` | Класичний upgrade за `UpgradeCode` ([`Package.wxs`](ResourceCalculator.Installer/Package.wxs)), GPO/SCCM. |
| **Windows** | Avalonia (альтернатива) | `publish/ITE.ResourceCalculator-avalonia-win-x64.zip` | Та сама логіка, але на Avalonia — для уніфікації UI. |

> Всі артефакти публікуються автоматично в GitHub Release командою [`release.ps1`](release.ps1).
> Поточна версія показана в шапці вікна (поруч із підзаголовком).

### 🔏 Цифровий підпис exe

[`sign.ps1`](sign.ps1) підписує опублікований `publish\ITE.ResourceCalculator.exe`:

```powershell
# Самопідписаний сертифікат (для внутрішнього використання)
./sign.ps1

# Корпоративний / придбаний сертифікат — підпис, якому довірятимуть інші ПК
./sign.ps1 -PfxPath C:\certs\company.pfx -PfxPassword (Read-Host -AsSecureString)
```

> Самопідписаний підпис вбудовується у файл, але на чужих ПК SmartScreen усе одно попереджатиме, доки сертифікат не додано в їхній Trusted Root (`-TrustLocally` додає його лише на поточну машину). Для зникнення попереджень потрібен сертифікат від CA.

---

## 🏷️ Версійність (обов'язково для кожного релізу)

Обидва канали оновлення покладаються на версію: `UpdateCheckService` порівнює її з тегом GitHub Release, а MSI — `ProductVersion` для upgrade/downgrade-логіки:

- **Єдине джерело версії** — `AppVersion` у [`Directory.Build.props`](Directory.Build.props).
- **Перед кожним релізом бампати `AppVersion`.** Інакше `UpdateCheckService` не побачить новий реліз, а публікація тегу, який уже існує, — помилка.
- [`release.ps1`](release.ps1) примусово перевіряє це: зупиняє реліз, якщо тег `vX.Y.Z` для поточної `AppVersion` уже є локально або на origin.

### 🚀 Публікація релізу

```powershell
git add <файли змін> && git commit -m "..."   # спершу закомітити зміни вручну
./release.ps1 -ReleaseNotes "Опис змін..."
```

Скрипт: перевіряє версійність → зупиняється при незакомічених/невідстежуваних змінах → білдить і тестує → публікує **WPF** (`win-x64`) + **Avalonia** (`win-x64`) → підписує exe → збирає MSI → пушить і тегує → створює GitHub Release з **3 артефактами** (`.exe`, `.msi`, `avalonia-win.zip`).

---

## 🧩 Технології

**Платформа:** C# / WPF (Windows) + Avalonia (Windows) · .NET 10 · MVVM
**Архітектура:** Microsoft.Extensions.DependencyInjection (DI-контейнер), `ResourceCalculator.Core` (спільна логіка)
**Excel/PDF:** EPPlus 7.6 / QuestPDF 2026.6
**Тести:** xUnit (131 тест) + збір звітів покриття (ReportGenerator)

---

## 📁 Структура проєкту

```
ResourceCalculator.slnx
├── Directory.Build.props            # спільна версія (AppVersion)
├── ResourceCalculator/            # WPF-застосунок (Windows)
│   ├── Models/                      # моделі даних (вузли, діапазони, модулі)
│   ├── Services/                    # рушій сайзингу, експорт, валідація, диски
│   ├── ViewModels/                  # ViewModel'и MVVM
│   ├── Views/                       # XAML-вкладки інтерфейсу
│   ├── Data/                        # матриця сайзингу за замовчуванням
│   ├── Localization/                # рядки інтерфейсу (uk/en)
│   └── Themes/                      # теми оформлення
├── ResourceCalculator.Avalonia/   # Avalonia-застосунок (Windows)
│   ├── Views/                       # AXAML-вкладки (порт WPF)
│   ├── Dialogs/                     # AvaloniaDialogService
│   └── packaging/                   # налаштування пакування
├── ResourceCalculator.Core/       # спільна логіка (SizingEngine, DataService…)
├── ResourceCalculator.Tests/      # модульні тести (xUnit, 131)
├── ResourceCalculator.Installer/  # WiX-проєкт MSI-інсталятора (Package.wxs)
├── docs/                            # банер та скріншоти
├── release.ps1                      # публікація GitHub Release (3 артефакти)
└── sign.ps1                         # цифровий підпис exe (Windows)
```

---

## 📄 Історія змін

Повний журнал змін — у [CHANGELOG.md](CHANGELOG.md).

---

## 📜 Ліцензія

[MIT](LICENSE) © [ajjs1ajjs](https://github.com/ajjs1ajjs)

---

<div align="center">

**IT-Enterprise Resource Calculator** © [ajjs1ajjs](https://github.com/ajjs1ajjs)

⭐ Сподобалось? Поставте зірочку!

</div>
