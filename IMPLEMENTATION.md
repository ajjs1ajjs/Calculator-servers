# IMPLEMENTATION.md — Деталі реалізації

> **Призначення**: Як працюють сервіси, рушій розрахунку, експорт, валідація та інші компоненти.
> Використовуй цей файл для розуміння внутрішньої логіки без читання вихідного коду.

<!-- AUTO:stamp -->Verified: 2026-09-17, commit `641371d` (scripts/Update-Docs.ps1)<!-- /AUTO -->

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

**Файли**: `ConfigExportService.cs` (фасад) → `PdfReportBuilder.cs`, `ExcelReportBuilder.cs`, `ReportCommon.cs`
**Залежності**: EPPlus (XLSX), QuestPDF (PDF) — див. нотатку про ліцензії в README «Ліцензії залежностей»

> Було одним класом на ~1000 рядків із двома бібліотеками всередині; правка одного
> звіту змушувала читати обидва. Публічний API фасада не змінився.
> Тексти звітів — завжди українською, незалежно від мови UI (клієнтські документи).

### ExportExcel(req, config, envReports)
Створює Excel-робочий аркуш:

**Аркуші**:
1. **Підсумок** — KPI: користувачі, тип розгортання, СУБД, CPU/RAM/Storage/IOPS
2. **Середовища** — порівняльна таблиця PROD/DEV/TEST/PreProd
3. **ВМ по середовищах** — розбивка вузлів для кожного середовища
4. **Компоненти по середовищах** — поди/компоненти для кожного середовища
5. **Інфраструктура** — деталізована таблиця вузлів з дисками (заголовки блоків включають к-сть користувачів)
6. **Компоненти** — деталізована таблиця компонентів

**Кольорова палітра** (Catppuccin Latte):
- Accent: #1E66F5 (синій)
- Success: #40A02B (зелений)
- Danger: #D20F39 (червоний)

### ExportPdf(req, config, envReports)
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

**Файл**: `ResourceCalculator.Core/Services/AccessService.cs`
**Залежності**: `Rfc2898DeriveBytes.Pbkdf2`, `RandomNumberGenerator`
**Шляхи**: `%LOCALAPPDATA%\ResourceCalculator\data\settings.json` (хеш) і `lockout.json` (спроби)

> ⚠️ Конкретних паролів у документації немає й не має бути: репозиторій публічний.
> Значення живе тільки у `settings.json` на машині користувача (ACL — лише поточний
> обліковий запис), у вигляді PBKDF2-хеша.

### Verify(password) / Verify(SecureString)
```
1. Перевірити блокування (lockout.json — переживає перезапуск процесу)
2. Завантажити settings.json (MatrixPasswordHash + MatrixPasswordSalt + Iterations)
3. Fail closed: немає файла або полів → відмова + реєстрація невдалої спроби
4. PBKDF2-SHA256(password, salt, Iterations, 32 байти)
   Легасі-файли без поля Iterations: single-round SHA-256 лише для міграції,
   після успіху пароль одразу перехешовується в PBKDF2
5. Порівняти через CryptographicOperations.FixedTimeEquals (constant-time)
```
Перевантаження з `SecureString` не створює immutable-копій пароля в купі:
BSTR → pinned `char[]` → UTF-8 байти, усе зануляється у `finally`.

### SetPassword(newPassword)
```
1. Відкинути пароль коротше 12 символів (виняток, не тихий no-op)
2. Згенерувати випадковий salt (16 байт)
3. PBKDF2-SHA256 з 210 000 ітерацій
4. Атомарний запис у settings.json + ACL «тільки поточний користувач»
```

### IsPasswordSet
`true` лише якщо у файлі справді є непорожні `MatrixPasswordHash` і
`MatrixPasswordSalt`. Раніше перевірялося саме існування файла: обірваний
`settings.json` (файл є, полів немає) переводив UI у стан «пароль встановлено», а
`Verify` завжди повертав false — доступ до матриці губився назовсім, без шляху
відновлення з інтерфейсу.

### EnsureInitialized()
Створює лише каталог даних. Вбудованого дефолтного пароля **немає** (fail closed):
за відсутності `settings.json` перший запуск показує режим СТВОРЕННЯ пароля.

### Блокування після невдалих спроб
Прогресивна затримка 1с→2с→4с…→30с, після 10 спроб — блок на 5 хвилин. Лічильник і
час лежать у `lockout.json` поряд із хешем: раніше вони жили лише в пам'яті процесу,
тож і затримка, і блок обходилися звичайним перезапуском програми. Час із файла
обрізається максимальним вікном блокування, щоб підправлений вручну `LockoutUntilUtc`
не заблокував доступ назовсім.

### GetPasswordHint()
Повертає контакти підтримки — **лише пошти** (телефон свідомо не показуємо).

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

### UpdateAsync(info, cancellationToken)
```
0. Відкинути, якщо UpdateInfo.Sha256 не є повним sha256 (fail closed ДО завантаження)
1. Завантажити EXE з info.DownloadUrl (300с таймаут, allowlist хостів GitHub, ліміт 500 МБ)
2. Записати у тимчасовий файл з випадковим іменем (проти squat/TOCTOU у %TEMP%)
3. Перевірити SHA-256 проти info.Sha256 (VerifyHashAsync — лише диск, без мережі)
4. Згенерувати .bat скрипт для заміни:
   - tasklist.exe чекає вихід процесу
   - Копіює новий EXE замість старого
   - Видаляє тимчасовий файл
   - Rollback при помилці
5. Запустити .bat і вийти з програми
```

### Прогрес
Подія `Progress` звітує про завантаження кожні ~100мс.

### Два бар'єри цілісності
**1. SHA-256 проти `UpdateInfo.Sha256`** — очікуваний хеш приходить із поля `digest`
того самого ассета GitHub API, з якого взято URL. Захищає від псування в дорозі
(обрив, проксі, підміна на шляху), але **не** від компрометації релізу: хеш і файл
з одного джерела. Раніше було гірше — `VerifyHashAsync` робив другий запит до
`/releases/latest` уже після завантаження (rate-limit + вікно, в яке «latest» ставав
наступним релізом, через що перевірка падала на коректному файлі).

**2. Authenticode-підпис — обов'язковий** (`VerifyAuthenticode`). Оце й закриває
компрометацію релізу: щоб підсунути оновлення, треба мати приватний ключ, а не лише
доступ до релізів чи до каналу завантаження.

```
1. OperatingSystem.IsWindows() — інакше відмова (перевіряти нечим → не підміняємо exe)
2. Витягнути сертифікат підписанта, порахувати SHA-256 відбиток
3. Звірити відбиток з піном SelfUpdateService.SigningCertSha256Thumbprint (сталий час)
4. WinVerifyTrust (WINTRUST_ACTION_GENERIC_VERIFY_V2, WTD_UI_NONE, WTD_REVOKE_NONE)
   0                      → підпис валідний і корінь довірений
   CERT_E_UNTRUSTEDROOT   → приймається ЛИШЕ разом з (3): сертифікат самопідписаний
   будь-що інше           → відмова (TRUST_E_NOSIGNATURE, TRUST_E_BAD_DIGEST,
                             CERT_E_EXPIRED, CERT_E_REVOKED, …)
```

Обидва бар'єри — пін і `WinVerifyTrust` — мусять пройти. `WinVerifyTrust` доводить,
що підпис математично валідний і файл не змінювали після підписання (один інший байт →
`TRUST_E_BAD_DIGEST`). Пін доводить, що підписали саме ми: без нього валідний підпис
**чужим** сертифікатом (хоч і виданим публічною CA) вважався б своїм.

Раніше цей крок звався `VerifyAuthenticodeIfPresent` і був нефатальним — лише писав у
лог. Тобто непідписаний або підписаний будь-ким exe спокійно підміняв робочу програму.

**Чим підписуємо.** Самопідписаний сертифікат (`CN=IT-Enterprise Resource Calculator`,
RSA-3072/SHA-256, до 2036 року). Публічної CA немає свідомо — для внутрішнього
інструменту довіру несе пін у коді, а не системне сховище, тож ставити корінь у
Trusted Root на машинах не потрібно. Ревокація не перевіряється: у самопідписаного
сертифіката немає ні CRL, ні OCSP, і така перевірка або впала б, або полізла в мережу
під час оновлення — «відкликання» тут робиться зміною піна й новим релізом.

**Заміна сертифіката** (пара кроків, обидва обов'язкові):
1. Новий PFX → секрети репозиторію `SIGNING_PFX_BASE64` + `SIGNING_PFX_PASSWORD`.
2. Новий SHA-256 відбиток → `SelfUpdateService.SigningCertSha256Thumbprint`.

Розійдуться — реліз впаде на кроці «Verify signature with the app's own check», який
виконує рівно той самий код, що й застосунок. Це свідомий гейт: реліз, який ніхто не
зможе встановити, гірший за відсутність релізу.

**Що лишається відкритим.** SmartScreen попереджатиме далі — це лікує лише сертифікат
від публічної CA з репутацією, а не самопідписаний. `release.yml` також публікує
provenance-атестацію (`actions/attest-build-provenance`), яку застосунок не перевіряє;
після обов'язкового Authenticode вона потрібна вже радше для аудиту.

---

## 10. UpdateCheckService — перевірка оновлень

**Файл**: `ResourceCalculator.Core/Services/UpdateCheckService.cs` (145 рядків)
**Інтерфейс**: `IUpdateCheckService`

### CheckForUpdateAsync()
```
1. GET https://api.github.com/repos/ajjs1ajjs/Calculator-servers/releases/latest
2. Retry 3 рази з backoff (1с, 2с)
3. Порівняти версії (парсинг Major.Minor.Patch, префікс v/суфікс +build відкидаються)
4. Знайти в assets прямий browser_download_url ITE.ResourceCalculator.exe (+size, +digest) та body (нотатки)
5. Повернути UpdateCheckResult(UpdateAvailable, UpdateInfo(Version, DownloadUrl, ReleaseNotes, SizeBytes, Sha256))
   або NoUpdate / Failed (мережа, rate-limit, немає ассета — не кидає, пише в update-check.log)
```

**Таймаут**: 15 секунд.

> Повний флоу оновлення всередині програми: `App.CheckForUpdatesAsync` → `Views/UpdateAvailableDialog` → `App.StartUpdateAsync` → `SelfUpdateService` → `Views/UpdateProgressDialog` (retry-цикл). Деталі — FUNCTIONS.md §9–10, §21.

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

**Файл**: `ResourceCalculator.Core/ViewModels/MainViewModel.cs`

### ValidateInputs()
Повертає текст помилки або `null`. `CalculateAsync` викликає його **перед** розрахунком
і на помилці показує діалог, не рахуючи нічого. Раніше `GetConfig` на будь-яке
нечислове/порожнє/нульове значення тихо брав 100 користувачів, а на сміття в обсягах
БД — нулі: програма видавала правдоподібний звіт для зовсім іншого розміру, і в
готовому PDF у клієнта це вже ніяк не видно. Межі — `MinUserCount=1`,
`MaxUserCount=5000` (діапазон, у якому визначена матриця).

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

**Файл**: `ResourceCalculator.Core/ViewModels/MatrixViewModel.cs`

### Ключові властивості
- `MsSqlRanges`, `AppServerRanges`, `WebServerRanges`, `PostgresRanges`, `OracleRanges`
- `K8sDocumentFlowComponents`
- `InfraNodes`, `WindowsInfraNodes`, `OptionalInfraNodes`
- `Engine` (EngineSettings)

### Ключові команди
- `SaveMatrixCommand` — збереження
- `RecalculateMatrixCommand` — перерахунок
- `ResetMatrixCommand` — скидання до дефолтів

Команди додавання рядка немає: модель матриці має рівно вісім слотів вузлів (`NodeSlot`),
тож рядок без слота нікуди не зберігався б. Колишні `AddRowCommand`/`AddRowAsync` не були
прив'язані ні до одного елемента XAML — мертвий код, що виглядав як робоча функція.

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
