# ARCHITECTURE.md — Архітектура проєкту

> **Призначення**: Загальний огляд архітектури, структури рішення, залежностей та патернів.
> Використовуй цей файл для розуміння як проєкт організований і як компоненти пов'язані.

---

## Зміст

1. [Мета проєкту](#1-мета-проєкту)
2. [Структура рішення](#2-структура-рішення)
3. [Технологічний стек](#3-технологічний-стек)
4. [Патерн архітектури](#4-патерн-архітектури)
5. [Залежності між проєктами](#5-залежності-між-проєктами)
6. [DI-контейнер](#6-di-контейнер)
7. [Збереження даних](#7-збереження-даних)
8. [CI/CD](#8-cicd)
9. [Пов'язані файли](#9-повязані-файли)

---

## 1. Мета проєкту

**IT-Enterprise Калькулятор ресурсів** — Windows desktop-додаток для автоматизованого розрахунку ресурсів ІТ-інфраструктури. Розраховує CPU, RAM, диски та IOPS на основі конфігурованої матриці розмірування з документа D-AD-ADM-E.

**Ключові можливості**:
- 3 типи розгортання: Kubernetes, Windows VM, Hybrid
- 3 СУБД: MS SQL Server, PostgreSQL, Oracle 19c
- 4 середовища: PROD, DEV, TEST, PreProd
- Експорт у Excel (.xlsx) та PDF
- Самооновлення через GitHub Releases
- Локалізація (українська / англійська)
- Теми: Catppuccin Latte (світла) + Catppuccin Mocha (темна)

---

## 2. Структура рішення

```
ResourceCalculator.slnx                     -- Рішення (.NET 10 XML-формат)
│
├── ResourceCalculator/                      -- WPF UI проєкт (тільки Windows)
│   ├── App.xaml / App.xaml.cs               -- Точка входу, DI-контейнер
│   ├── MainWindow.xaml / .cs                -- Головне вікно (3 вкладки)
│   ├── Views/                               -- User Controls (вкладки)
│   │   ├── MatrixTabControl.xaml            -- Вкладка 1: Редагування матриці
│   │   ├── CalculatorTabControl.xaml        -- Вкладка 2: Параметри розрахунку
│   │   ├── ResultsTabControl.xaml           -- Вкладка 3: Результати
│   │   ├── PasswordDialog.xaml              -- Діалог пароля
│   │   └── UpdateProgressDialog.xaml        -- Діалог оновлення
│   ├── Converters/                          -- WPF конвертери значень
│   ├── Dialogs/                             -- Реалізації IDialogService для WPF
│   ├── Themes/                              -- XAML-ресурси тем (Catppuccin)
│   └── Localization/                        -- LocExtension для XAML-біндінгів
│
├── ResourceCalculator.Core/                 -- Бібліотека бізнес-логіки (UI-агностик)
│   ├── Data/                                -- Дані матриці та еталонні вимоги
│   │   ├── SizingMatrix.cs                  -- Повна матриця розмірування (JSON)
│   │   └── DocumentRequirements.cs          -- Еталон з документа D-AD-ADM-E
│   ├── Models/                              -- Моделі даних (13 файлів)
│   ├── Interfaces/                          -- Абстракції сервісів (10 файлів)
│   ├── Services/                            -- Реалізація сервісів (14 файлів)
│   ├── ViewModels/                          -- MVVM ViewModels (4 файли)
│   └── Localization/                        -- Сервіс локалізації (243+ ключі)
│
└── ResourceCalculator.Tests/                -- Unit-тести (xUnit)
    ├── SizingEngineTests.cs                 -- 57 тестів (найбільший файл)
    ├── MainViewModelTests.cs                -- 14 тестів
    ├── ValidationEngineTests.cs             -- 7 тестів
    └── ... (9 файлів загалом)
```

---

## 3. Технологічний стек

| Категорія | Технологія | Версія |
|---|---|---|
| **Runtime** | .NET | 10.0 (SDK 10.0.302) |
| **UI Framework** | WPF | net10.0-windows |
| **Архітектура** | MVVM | + DI |
| **DI Container** | Microsoft.Extensions.DependencyInjection | 10.0.9 |
| **Excel** | EPPlus | 7.6.0 |
| **PDF** | QuestPDF | 2026.6.1 |
| **Тести** | xUnit | 2.9.3 |
| **Coverage** | coverlet.collector | 6.0.4 |
| **CI/CD** | GitHub Actions | — |
| **Збірка** | Single-file self-contained | win-x64 |

---

## 4. Патерн архітектури

### MVVM (Model-View-ViewModel)

**View** (WPF проєкт):
- XAML-вю з data binding до ViewModel
- Конвертери для UI-логіки
- Мінімум code-behind (тільки оновлення + scroll)

**ViewModel** (Core проєкт):
- `MainViewModel` — головний: таби, розрахунок, експорт, історія, мова/тема
- `MatrixViewModel` — редагування матриці: діапазони, компоненти, вузли
- Реалізують `INotifyPropertyChanged`
- Команди: `RelayCommand`, `AsyncRelayCommand`

**Model** (Core проєкт):
- Дані: `SizingMatrix`, `ProjectConfig`, `ResourceRequirement`, `InfrastructureNode` тощо
- Бізнес-логіка: `SizingEngine`, `ValidationEngine`, `ReplicaMath`

### Інтерфейси (контракти сервісів)

Усі сервіси визначені як інтерфейси в `ResourceCalculator.Core/Interfaces/`:
- `ISizingEngine` — розрахунок ресурсів
- `IValidationEngine` — валідація
- `IDataService` — збереження JSON
- `IDialogService` — діалоги
- `IFileSaveService` — збереження файлів
- `ILocalizationService` — локалізація
- `IThemeService` — теми
- `ICalculationHistoryService` — історія
- `IUpdateCheckService` — перевірка оновлень
- `ISelfUpdateService` — саме оновлення

**WPF реалізації** підключаються в DI-контейнері: `WpfDialogService`, `WpfThemeService`.

---

## 5. Залежності між проєктами

```
ResourceCalculator (WPF)
    └──参照──→ ResourceCalculator.Core
                    ↑
ResourceCalculator.Tests
    └──参照──→ ResourceCalculator.Core
```

- **Core** не залежить від жодного UI-фреймворку
- **WPF** містить тільки UI: вю, конвертери, теми, WPF-діалоги
- **Tests** тестують тільки Core

---

## 6. DI-контейнер

Реєстрація в `App.xaml.cs`:

```csharp
// Singleton (один на всю програму)
services.AddSingleton<ILocalizationService>(LocalizationService.Instance);
services.AddSingleton<SizingMatrix>();
services.AddSingleton<MatrixManager>();
services.AddSingleton<AccessService>();
services.AddSingleton<IThemeService, WpfThemeService>();
services.AddSingleton<ISizingEngine>(sp => new SizingEngine(mm.Matrix));
services.AddSingleton<IUpdateCheckService, UpdateCheckService>();
services.AddSingleton<ISelfUpdateService, SelfUpdateService>();

// Transient (новий екземпляр кожного запиту)
services.AddTransient<IDataService, DataService>();
services.AddTransient<ICalculationHistoryService, CalculationHistoryService>();
services.AddTransient<IValidationEngine, ValidationEngine>();
services.AddTransient<ConfigExportService>();
services.AddTransient<ResultsPresenter>();
services.AddTransient<EnvironmentBuilder>();
services.AddTransient<MainViewModel>();
```

**Ключові Singleton**:
- `SizingMatrix` — єдина матриця на весь час життя
- `MatrixManager` — CRUD для матриці
- `AccessService` — пароль (SHA-256 + salt)
- `SizingEngine` — рушій розрахунку (отримує Matrix з DI)

---

## 7. Збереження даних

### Файли (JSON)

| Файл | Шлях | Опис |
|---|---|---|
| `matrix.json` | `%LOCALAPPDATA%\ResourceCalculator\data\` | Матриця розмірування |
| `settings.json` | `%LOCALAPPDATA%\ResourceCalculator\data\` | Хеш пароля + salt |
| `history.json` | `%LOCALAPPDATA%\ResourceCalculator\` | Останні 20 розрахунків |

### Атомарний запис
`DataService.SaveMatrix()` використовує патерн:
1. Запис у `.tmp` файл
2. Перейменування існуючого у `.bak`
3. Перейменування `.tmp` у основний

### Версіонування схеми
`SizingMatrix.CurrentSchemaVersion = 10`. Якщо збережена матриця має `SchemaVersion < 10` — вона відкидається і створюється нова з дефолтними значеннями.

---

## 8. CI/CD

### GitHub Actions workflows

| Workflow | Тригер | Призначення |
|---|---|---|
| `ci.yml` | Push to main, PRs | Build + Test + Coverage + Vulnerability check |
| `release.yml` | Push tag `v*` | Build + Test + Publish EXE + GitHub Release |
| `pages.yml` | Push to main | Deploy landing page to GitHub Pages |

### Процес релізу
1. Змінити `AppVersion` в `Directory.Build.props`
2. `git commit && git push`
3. `git tag vX.Y.Z && git push origin vX.Y.Z`
4. GitHub Actions автоматично створить реліз з EXE

---

## 9. Пов'язані файли

| Документ | Посилання | Опис |
|---|---|---|
| DATA-MODELS.md | [→](DATA-MODELS.md) | Детальний опис моделей даних |
| IMPLEMENTATION.md | [→](IMPLEMENTATION.md) | Деталі реалізації сервісів |
| FUNCTIONS.md | [→](FUNCTIONS.md) | Публічні методи та API |
| TESTS.md | [→](TESTS.md) | Структура та покриття тестів |
