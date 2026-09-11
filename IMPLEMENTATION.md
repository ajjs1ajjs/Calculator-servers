# IMPLEMENTATION.md — Деталі реалізації

> **Призначення**: Як працюють сервіси, рушій розрахунку, експорт, валідація та інші компоненти.
> Використовуй цей файл для розуміння внутрішньої логіки без читання вихідного коду.

---

## Зміст

1. [SizingEngine — рушій розрахунку](#1-sizingengine--рушій-розрахунку)
2. [DataService — збереження JSON](#2-dataservice--збереження-json)
3. [MatrixManager — управління матрицею](#3-matrixmanager--управління-матрицею)
4. [ConfigExportService — експорт Excel/PDF](#4-configexportservice--експорт-excelpdf)
5. [ValidationEngine — валідація ресурсів](#5-validationengine--валідація-ресурсів)
6. [EnvironmentBuilder — побудова середовищ](#6-environmentbuilder--побудова-середовищ)
7. [AccessService — захист паролем](#7-accessservice--захист-паролем)
8. [CalculationHistoryService — історія](#8-calculationhistoryservice--історія)
9. [SelfUpdateService — самооновлення](#9-selfupdateservice--самооновлення)
10. [UpdateCheckService — перевірка оновлень](#10-updatecheckservice--перевірка-оновлень)
11. [LocalizationService — локалізація](#11-localizationservice--локалізація)
12. [DiskAdvisor — рекомендації дисків](#12-diskadvisor--рекомендації-дисків)
13. [ResultsPresenter — фасад результатів](#13-resultspresenter--фасад-результатів)
14. [MainViewModel — головний ViewModel](#14-mainviewmodel--головний-viewmodel)
15. [MatrixViewModel — ViewModel матриці](#15-matrixviewmodel--viewmodel-матриці)

---

## 1. SizingEngine — рушій розрахунку

**Файл**: `ResourceCalculator.Core/Services/SizingEngine.cs` (627 рядків)
**Інтерфейс**: `ISizingEngine`
**Залежності**: `SizingMatrix`

### Метод `Calculate(config)`

 головний вхідний метод. Вибирає стратегію за `DeploymentType`:

```
Calculate(config)
├── Kubernetes → CalculateK8s(config)
├── Windows    → CalculateWindows(config)
└── Hybrid     → CalculateHybrid(config)
```

### CalculateK8s(config)

1. Знаходить діапазон з `MsSqlRanges` за `UserCount`
2. Створює SQL-вузол (`_defaultSql` + параметри з діапазону)
3. Додає Master-вузол (`_defaultMaster`)
4. Додає Worker-вузли (`_defaultWorker`)
5. Розраховує кількість Worker: `Ceiling(Sum(pod.Cpu) / worker.Cpu)`
6. Додає SmartID (0.2 CPU / 0.5 GB на кожні 25 користувачів)
7. Для кожного модуля з `DocumentFlowModules`:
   - Рахує репліки через `ReplicaMath.Resolve()`
   - Множить CPU/RAM компонента × репліки
   - Додає до загального запиту подів
8. Викликає `AddOptionalNodes()` для опціональних вузлів
9. Викликає `ApplyDbDisks()` для розбиття дисків БД

### CalculateWindows(config)

1. Знаходить діапазон SQL з `MsSqlRanges`
2. Знаходить діапазон App з `AppServerRanges` (кількість VM = `InstanceCount`)
3. Знаходить діапазон Web з `WebServerRanges` (кількість VM = `InstanceCount`)
4. Рахує pagefile: `CEILING(RAM × PageFileMultiplier, PageFileRounding)`
5. Додає опціональні вузли
6. Розбиває диски БД

### CalculateHybrid(config)

1. K8s-частина: ForceBPM + інші K8s-сервіси
2. Windows-частина: SQL + App + Web
3. Дедуплікація спільного SQL-вузла
4. HAProxy тільки для Hybrid

### AddOptionalNodes(config, result)

- `IncludeReportingServer` → додає `_defaultReporting`
- `IncludeSqlFailover` → додає другий SQL-вузол
- `IncludeHaProxy` → додає HAProxy (тільки K8s/Hybrid)

### ApplyDbDisks(result, config)

Розбиває диск SQL-вузла на 4 розділи:
- **Диск 1 (OS)**: 100 ГБ (non-prod) / фіксоване значення (PROD)
- **Диск 2 (Logs+TempDB)**: з матриці
- **Диск 3 (MainData)**: з матриці або `DbSizeGb × 1024`
- **Диск 4 (Content)**: з матриці або `ContentDbSizeGb × 1024` (тільки PROD)

### DbVersionLabel(config)

Визначає редакцію СУБД:
- `DbSizeGb > MsSqlStandardMaxRamGb` або `TotalCpu > MsSqlStandardMaxCores` → `"Enterprise"`
- `Environment != Prod` → `"Developer Edition"`
- Інакше → `"Standard"`

---

## 2. DataService — збереження JSON

**Файл**: `ResourceCalculator.Core/Services/DataService.cs` (109 рядків)
**Інтерфейс**: `IDataService`
**Шлях**: `%LOCALAPPDATA%\ResourceCalculator\data\matrix.json`

### SaveMatrix(matrix)
```
1. Серіалізація matrix → JSON
2. Запис у matrix.json.tmp
3. Якщо matrix.json існує → перейменувати у matrix.json.bak
4. Перейменувати matrix.json.tmp → matrix.json
```

### LoadMatrix()
```
1. Якщо файл не існує → створити нову SizingMatrix() з дефолтами
2. Десеріалізувати JSON
3. Перевірити SchemaVersion: якщо < CurrentSchemaVersion (10) → відхилити, створити нову
4. Повернути matrix
```

### ClearMatrix()
Видаляє `matrix.json` (якщо існує).

---

## 3. MatrixManager — управління матрицею

**Файл**: `ResourceCalculator.Core/Services/MatrixManager.cs` (174 рядки)
**Залежності**: `IDataService`, `SizingMatrix`

### Save()
Зберігає поточний стан матриці через `DataService`.

### Reset()
Скидає матрицю до дефолтних значень (новий `SizingMatrix()`).

### SyncGridsToMatrix()
Синхронізує дані з UI-грідів назад у `SizingMatrix`. Викликається після редагування матриці.

### CopyMatrix()
Глибока копія матриці (для безпечного редагування).

### NormalizeModulePolicy()
Встановлює `IsMandatory` та `IsKubernetesOnly` для модулів:
- App Server, ROBOT, Web → `IsMandatory = true`
- ForceBPM → `IsKubernetesOnly = true`
- LMS, HR Portal → за замовчуванням вимкнені

---

## 4. ConfigExportService — експорт Excel/PDF

**Файл**: `ResourceCalculator.Core/Services/ConfigExportService.cs` (970 рядків)
**Залежності**: EPPlus, QuestPDF

### ExportExcel(config, requirements, envReports)
Створює Excel-робочий аркуш:

**Аркуші**:
1. **Підсумок** — KPI: користувачі, тип розгортання, СУБД, CPU/RAM/Storage/IOPS
2. **Середовища** — порівняльна таблиця PROD/DEV/TEST/PreProd
3. **ВМ по середовищах** — розбивка вузлів для кожного середовища
4. **Компоненти по середовищах** — поди/компоненти для кожного середовища
5. **Інфраструктура** — деталізована таблиця вузлів з дисками
6. **Компоненти** — деталізована таблиця компонентів

**Кольорова палітра** (Catppuccin Latte):
- Accent: #1E66F5 (синій)
- Success: #40A02B (зелений)
- Danger: #D20F39 (червоний)

### ExportPdf(config, requirements, envReports)
Створює PDF-документ (A4, landscape):

**Секції**:
1. KPI-картки (користувачі, розгортання, СУБД, середовище)
2. Таблиця інфраструктури з розбиттям дисків
3. Компоненти (поди)
4. Глосарій термінів

---

## 5. ValidationEngine — валідація ресурсів

**Файл**: `ResourceCalculator.Core/Services/ValidationEngine.cs` (140 рядків)
**Інтерфейс**: `IValidationEngine`
**Залежності**: `ILocalizationService`

### Validate(required, allocated)
Порівнює потрібні vs виділені ресурси. Повертає `List<ValidationResult>`.

### ValidateProject(config, calculated, actualResources)
Валідує розраховані ресурси проти фактичних вузлів.

### CompareProfiles(profile1, profile2)
Порівнює два профілі ресурсів (наприклад, PROD vs розрахунок).

### Пороги Severity
| Умова | Severity | Рекомендація |
|---|---|---|
| `DeltaPercent < -20%` | **Critical** | "Недостатньо ресурсів! Потрібно збільшити..." |
| `-20% ≤ DeltaPercent < 0` | **Warning** | "Ресурси на межі..." |
| `DeltaPercent > 50%` | **Overprovisioned** | "Забагато ресурсів! Можна зменшити..." |
| `0 ≤ DeltaPercent ≤ 50%` | **OK** | — |

---

## 6. EnvironmentBuilder — побудова середовищ

**Файл**: `ResourceCalculator.Core/Services/EnvironmentBuilder.cs` (179 рядків)
**Інтерфейс**: —
**Залежності**: `ISizingEngine`, `ILocalizationService`

### ParseSettings(config, envSettings, moduleCounts)
Перетворює UI-налаштування на `ProjectConfig` для кожного середовища.

### Build(config, envSettings, moduleCounts)
Будує звіти для всіх середовищ:

```
Build()
├── PROD ( завжди )
├── PreProd (якщо IncludePredProd)
├── Test (якщо IncludeTest)
└── Dev (якщо IncludeDev)
```

### BuildEnv(envConfig)
Рахує одне середовище через `SizingEngine.Calculate()`.

**Порядок**: PROD → PreProd → Test → Dev (від найбільшого до найменшого).

**Кожне середовище** має:
- Власну кількість користувачів
- Власний обсяг БД
- Власні увімкнення модулів
- Власні додаткові вузли

---

## 7. AccessService — захист паролем

**Файл**: `ResourceCalculator.Core/Services/AccessService.cs` (109 рядків)
**Залежності**: `SHA256`, `RandomNumberGenerator`
**Шлях**: `%LOCALAPPDATA%\ResourceCalculator\data\settings.json`

### Verify(password)
```
1. Завантажити settings.json (hash + salt)
2. Обчислити SHA-256 від password + salt
3. Порівняти через CryptographicOperations.FixedTimeEquals (constant-time)
4. Повернути true/false
```

### SetPassword(newPassword)
```
1. Згенерувати випадковий salt (16 байт)
2. Обчислити SHA-256(newPassword + salt)
3. Зберегти у settings.json
```

### EnsureInitialized()
Якщо `settings.json` не існує — створює з паролем за замовчуванням: `yF2jrX7inC4w`.

### GetPasswordHint()
Повертає контакти розробника: email + телефон.

---

## 8. CalculationHistoryService — історія

**Файл**: `ResourceCalculator.Core/Services/CalculationHistoryService.cs` (67 рядків)
**Інтерфейс**: `ICalculationHistoryService`
**Шлях**: `%LOCALAPPDATA%\ResourceCalculator\history.json`
**Ліміт**: 20 записів

### SaveToHistory(config, req)
```
1. Створити CalculationHistoryItem
2. Додати на початок списку
3. Обрізати до 20 записів
4. Зберегти у JSON
```

### LoadHistory()
Завантажує список з JSON.

---

## 9. SelfUpdateService — самооновлення

**Файл**: `ResourceCalculator.Core/Services/SelfUpdateService.cs` (223 рядки)
**Інтерфейс**: `ISelfUpdateService`

### UpdateAsync(cancellationToken)
```
1. Завантажити EXE з DownloadUrl (300с таймаут)
2. Записати у тимчасовий файл
3. Перевірити SHA-256 хеш (VerifyHashAsync)
4. Згенерувати .bat скрипт для заміни:
   - tasklist.exe чекає вихід процесу
   - Копіює новий EXE замість старого
   - Видаляє тимчасовий файл
   - Rollback при помилці
5. Запустити .bat і вийти з програми
```

### Прогрес
Подія `Progress` звітує про завантаження кожні ~100мс.

---

## 10. UpdateCheckService — перевірка оновлень

**Файл**: `ResourceCalculator.Core/Services/UpdateCheckService.cs` (139 рядків)
**Інтерфейс**: `IUpdateCheckService`

### CheckForUpdateAsync()
```
1. GET https://api.github.com/repos/ajjs1ajjs/Calculator-servers/releases/latest
2. Retry 3 рази з backoff
3. Порівняти версії (парсингMajor.Minor.Patch)
4. Повернути UpdateCheckResult
```

**Таймаут**: 15 секунд.

---

## 11. LocalizationService — локалізація

**Файл**: `ResourceCalculator.Core/Localization/LocalizationService.cs` (521 рядок)
**Інтерфейс**: `ILocalizationService`

**Ключові можливості**:
- Singleton
- 2 словники: `StringsUk` (243 ключі), `StringsEn` (233 ключі)
- `this[key]` — індексатор для доступу до рядків
- `LoadLanguage(lang)` — перемикання мови
- `INotifyPropertyChanged` — для автоматичного оновлення UI

---

## 12. DiskAdvisor — рекомендації дисків

**Файл**: `ResourceCalculator.Core/Services/DiskAdvisor.cs` (83 рядки)
**Статичний клас**

### Build(node)
Форматує текстову рекомендацію щодо дисків для вузла:

- **SQL**: OS + Logs/TempDB + MainData + Content (з профілями IOPS)
- **App/Web**: OS + pagefile (якщо є)
- **Non-DB**: тільки OS

**Non-prod**: OS-диск зменшується до 100 ГБ, Content не виділяється.

---

## 13. ResultsPresenter — фасад результатів

**Файл**: `ResourceCalculator.Core/Services/ResultsPresenter.cs` (35 рядків)
**Залежності**: `ConfigExportService`, `IValidationEngine`

Тонка оболонка, що делегує:
- `Validate()` → `IValidationEngine.Validate()`
- `ValidateProject()` → `IValidationEngine.ValidateProject()`
- `CompareProfiles()` → `IValidationEngine.CompareProfiles()`
- `ExportExcel()` → `ConfigExportService.ExportExcel()`
- `ExportPdf()` → `ConfigExportService.ExportPdf()`

---

## 14. MainViewModel — головний ViewModel

**Файл**: `ResourceCalculator.Core/ViewModels/MainViewModel.cs` (675 рядків)

### Ключові властивості
- `UserCount`, `DeploymentType`, `DatabaseType`, `LoadProfile`
- `EnvironmentSettings`, `ModuleCounts`
- `TotalCpu`, `TotalRamGb`, `TotalStorageGb`, `TotalIops`
- `Results` (ResourceRequirement), `EnvReports` (List<EnvironmentReport>)
- `History`, `SelectableModules`

### Ключові команди
- `CalculateCommand` — запуск розрахунку
- `ExportExcelCommand` / `ExportPdfCommand` — експорт
- `ThemeSwitchCommand` / `LangSwitchCommand` — перемикання
- `RecallHistoryCommand` — відновлення з історії

### Calculate()
```
1. Побудувати ProjectConfig з UI-даних
2. Викликати EnvironmentBuilder.Build()
3. Отримати List<EnvironmentReport>
4. Оновити KPI (TotalCpu/Ram/Storage/Iops)
5. Зберегти в історію
6. Переключити на вкладку результатів
```

---

## 15. MatrixViewModel — ViewModel матриці

**Файл**: `ResourceCalculator.Core/ViewModels/MatrixViewModel.cs` (224 рядки)

### Ключові властивості
- `MsSqlRanges`, `AppServerRanges`, `WebServerRanges`, `PostgresRanges`, `OracleRanges`
- `K8sDocumentFlowComponents`
- `InfraNodes`, `WindowsInfraNodes`, `OptionalInfraNodes`
- `Engine` (EngineSettings)

### Ключові команди
- `SaveMatrixCommand` — збереження
- `RecalculateMatrixCommand` — перерахунок
- `ResetMatrixCommand` — скидання до дефолтів

### EnsureUnlocked()
Перевіряє пароль перед редагуванням матриці. Викликає `AccessService` → `PasswordDialog`.

---

## Потік даних

```
[UI Input] → MainViewModel → ProjectConfig
                                    ↓
                            EnvironmentBuilder
                                    ↓
                            SizingEngine.Calculate()
                                    ↓
                            ResourceRequirement
                                    ↓
                    ┌───────────────┼───────────────┐
                    ↓               ↓               ↓
            ValidationEngine  ConfigExportService  CalculationHistoryService
                    ↓               ↓               ↓
            [ValidationResult[]]  [Excel/PDF]     [history.json]
```
