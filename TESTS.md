# TESTS.md — Тести проєкту

> **Призначення**: Структура, покриття та опис кожного тестового файлу.
> Використовуй цей файл для швидкого розуміння які тести існують і що вони перевіряють.

<!-- AUTO:stamp -->Verified: 2026-09-15, commit `10355a9` (scripts/Update-Docs.ps1)<!-- /AUTO -->

---

## Зміст

1. [Загальна інформація](#1-загальна-інформація)
2. [SizingEngineTests](#2-sizingenginetests)
3. [MainViewModelTests](#3-mainviewmodeltests)
4. [ValidationEngineTests](#4-validationenginetests)
5. [ConfigExportServiceTests](#5-configexportservicetests)
6. [DocumentRequirementsTests](#6-documentrequirementstests)
7. [ResultsPresenterTests](#7-resultspresentertests)
8. [MatrixManagerTests](#8-matrixmanagertests)
9. [DiskAdvisorTests](#9-diskadvisortests)
10. [AccessServiceTests](#10-accessservicetests)
11. [SecurityRegressionTests](#11-securityregressiontests)

---

## 1. Загальна інформація

| Параметр | Значення |
|---|---|
| **Фреймворк** | xUnit 2.9.3 |
| **Coverage** | coverlet.collector 6.0.4 |
| **Проєкт** | ResourceCalculator.Tests (net10.0) |
| **Залежність** | ResourceCalculator.Core |
| **Файлів тестів** | 10 |
| **Загальна кількість тестів** | <!-- AUTO:tests-total -->157<!-- /AUTO --> (атрибутів `[Fact]`/`[Theory]` — 132, з них theory дають 25 кейсів через `[InlineData]`) |

---

## 2. SizingEngineTests

**Файл**: `ResourceCalculator.Tests/SizingEngineTests.cs` (1110 рядків)
**Тестів**: 65 атрибутів (найбільший файл; з theory-кейсами більше)
**Тестує**: `SizingEngine`

### Групи тестів

| Група | Тести | Що перевіряє |
|---|---|---|
| K8s базовий | 100 users, Performance | CPU/RAM/Storage/IOPS для K8s |
| K8s SmartID | Central SmartID | Ресурси SmartID на кожні 25 користувачів |
| K8s master node | 2/4 вузла | Кількість master-вузлів залежно від worker |
| K8s worker | Worker scaling | Автоматичне визначення кількості worker |
| K8s IOPS | IOPS profiles | Профілі читання/запису для K8s |
| Windows | 50 users | Розрахунок для Windows VM |
| Windows pagefile | PageFile | Формула CEILING(RAM×4, 10) |
| Hybrid | 200 users | Комбінований розрахунок |
| Hybrid дедуплікація | App/Web | Не подвоює спільні вузли |
| Optional nodes | Reporting, Failover, HAProxy | Опціональні вузли вимкнені за замовч. |
| HAProxy | K8s/Hybrid | HAProxy тільки для K8s/Hybrid |
| DB ranges | MS SQL, Postgres, Oracle | Пошук діапазону за кількістю користувачів |
| DB disks | Disk split | Розбиття дисків SQL (OS/Logs/Data/Content) |
| DB version | Enterprise/Standard/Developer | Визначення редакції СУБД |
| DB size scaling | DbSizeGb | Масштабування дисків за обсягом БД |
| ContentDbSize | ContentDbSizeGb | Масштабування Content |
| Components | Per-replica, total | Компоненти: CPU/RAM на репліку vs загальний |
| Module user count | LMS/HR Portal | Масштабування модулів з власною кількістю користувачів |
| Enterprise | >128GB RAM / >24 cores | MS SQL Enterprise при перевищенні лімітів |
| HR Portal load test | GraphQL, SmartID, ROBOT | Емпіричні формули з тестів |
| LMS load test | GraphQL breakpoints | Точна таблиця з тесту |
| Stale schema | SchemaVersion < 10 | Відхилення застарої матриці |
| Custom engine settings | Змінені константи | EngineSettings впливає на розрахунок |
| Pod requests | PodCpu/PodRamGb | Запити подів < фізичних ресурсів |

### Тестові хелпери

- `BuildConfig(...)` — створення ProjectConfig з параметрами
- `BuildDefaultMatrix()` — матриця за замовчуванням
- `BuildCustomMatrix(...)` — матриця з кастомними діапазонами

---

## 3. MainViewModelTests

**Файл**: `ResourceCalculator.Tests/MainViewModelTests.cs` (229 рядків)
**Тестів**: 14
**Тестує**: `MainViewModel`

| Тест | Що перевіряє |
|---|---|
| Constructor populates modules | Модулі завантажуються при створенні |
| DEV uses own module count | DEV використовує власну кількість користувачів модуля |
| Enabled modules auto-included | Увімкнені модулі автоматично додаються в похідні середовища |
| Disabling module excludes | Вимкнення модуля виключає його з похідних |
| Calculate produces results | Розрахунок генерує результати + перемикає вкладку |
| Saves to history | Результат зберігається в історію |
| Invalid user count fallback | Невалідна кількість користувачів → 0 |
| Windows VM infrastructure | Windows-розгортання генерує VM |
| K8s pod requests | K8s генерує под-запити |
| Hybrid HAProxy toggles | Hybrid увімкнення HAProxy |
| ForceBPM toggleability | ForceBPM можна вмикати/вимикати |
| Language switching | Перемикання мови оновлює UI |

### Тестові стаби
- `FakeDataService` — порожня матриця
- `FakeHistoryService` — зберігає в пам'ять

---

## 4. ValidationEngineTests

**Файл**: `ResourceCalculator.Tests/ValidationEngineTests.cs` (138 рядків)
**Тестів**: 8
**Тестує**: `ValidationEngine`

| Тест | Що перевіряє |
|---|---|
| All match → OK | Всі ресурси відповідають → Severity = OK |
| Insufficient CPU → Critical | CPU < 80% → Critical |
| Slightly under → Warning | CPU 85-100% → Warning |
| Overprovisioned | CPU > 150% → Overprovisioned |
| Missing infra → Critical | Відсутність вузлів → Critical |
| Matching infra → valid | Відповідні вузли → OK |
| Zero required | Ділення на нуль не викликає помилку |
| Severity edge cases | Граничні значення порогів severity |

---

## 5. ConfigExportServiceTests

**Файл**: `ResourceCalculator.Tests/ConfigExportServiceTests.cs` (147 рядків)
**Тестів**: 7
**Тестує**: `ConfigExportService`

| Тест | Що перевіряє |
|---|---|
| ExportPdf → non-empty PDF | PDF починається з `%PDF` |
| ExportPdf with environments | PDF з середовищами |
| ExportExcel → valid ZIP | Excel містить ZIP-архів |
| No separate disk sheet | Немає окремого аркуша для дисків |
| Infrastructure sheet has disks | Аркуш інфраструктури має колонки дисків |
| With environments | Excel з середовищами |
| Infrastructure covers all envs | Інфраструктура для всіх середовищ |

---

## 6. DocumentRequirementsTests

**Файл**: `ResourceCalculator.Tests/DocumentRequirementsTests.cs` (85 рядків)
**Тестів**: 6
**Тестує**: `DocumentRequirements`

| Тест | Що перевіряє |
|---|---|
| ForUsers picks range | Знаходить правильний діапазон за кількістю користувачів |
| Compare flags below | Позначає недостатні ресурси |
| Compare passes when meets | Проходить при відповідності |
| Compare fills matrix | Заповнює стовпчик з матриці |
| Without matrix → dash | Без матриці — тире |
| Non-SQL → empty | Для не-SQL БД — порожній результат |

---

## 7. ResultsPresenterTests

**Файл**: `ResourceCalculator.Tests/ResultsPresenterTests.cs` (120 рядків)
**Тестів**: 8
**Тестує**: `ResultsPresenter`

| Тест | Що перевіряє |
|---|---|
| CompareProfiles → results | Порівняння повертає результати |
| Detects differences | Знаходить різницю |
| Equal → OK | Однакові → OK |
| Under-allocated → critical | Недостатньо → Critical |
| ExportExcel → workbook | Excel створюється |
| ExportPdf → PDF | PDF створюється |
| Missing infra → critical | Відсутні вузли → Critical |
| All present → node validations | Повний набір вузлів → валідація по кожному |

---

## 8. MatrixManagerTests

**Файл**: `ResourceCalculator.Tests/MatrixManagerTests.cs` (92 рядки)
**Тестів**: 2
**Тестує**: `MatrixManager`

| Тест | Що перевіряє |
|---|---|
| Load restores module policy | IsMandatory/IsKubernetesOnly відновлюються з коду |
| SyncGridsToMatrix persists | Синхронізація зберігає всі діапазони, вузли, налаштування |

### Тестові стаби
- `StaleDataService` — матриця зі старою версією схеми
- `EmptyDataService` — порожня матриця

---

## 9. DiskAdvisorTests

**Файл**: `ResourceCalculator.Tests/DiskAdvisorTests.cs` (87 рядків)
**Тестів**: 6
**Тестує**: `DiskAdvisor`

| Тест | Що перевіряє |
|---|---|
| SQL without split | SQL без розбиття → текст з дисками |
| SQL on Windows → no page file | SQL на Windows немає pagefile |
| Node with IOPS | Вузол з IOPS → показує IOPS/латентність |
| App server with pagefile | App сервер з pagefile |
| SQL with matrix split | SQL з розбиттям з матриці |
| Non-DB node → OS disk | Не-БД вузол без розбиття все одно показує OS-диск |

---

## 10. AccessServiceTests

**Файл**: `ResourceCalculator.Tests/AccessServiceTests.cs`
**Тестів**: 7
**Тестує**: `AccessService`

| Тест | Що перевіряє |
|---|---|
| EnsureInitialized does not create password | Немає авто-дефолту: fail closed без settings.json |
| SetPassword round-trips with PBKDF2 | Запис/читання, поле Iterations |
| SetPassword rejects weak passwords | Короткі/порожні відхиляються |
| Verify legacy SHA-256 migrates | Старий хеш приймається раз і перехешовується |
| Verify throttles repeated failures | Прогресивна затримка блокує повтори |
| DevContacts contains email+phone | Контакти містять email і телефон |
| GetPasswordHint does not leak secret | Підказка без вбудованого пароля |

---

## 11. SecurityRegressionTests

**Файл**: `ResourceCalculator.Tests/SecurityRegressionTests.cs`
**Тестів**: 9 (із них 2 theory)
**Тестує**: `MatrixValidator`, `MatrixManager.SyncGridsToMatrix`, `ConfigExportService.Xl`, `SelfUpdateService.IsAllowedDownloadUrl`, `ValidationEngine`, `DataService`

| Тест | Що перевіряє |
|---|---|
| Validator rejects negative/inverted | Невалідні CPU та діапазони |
| Validator rejects NaN/overlaps | NaN і перетин діапазонів |
| Validator rejects zero rounding | PageFileRounding нуль (ділення) |
| Validator accepts defaults | Вбудована матриця проходить |
| Sync rejects invalid without mutating | Помилки синку без мутації стану |
| Xl neutralizes formula injection | Символи формул отримують префікс-апостроф |
| URL allowlist | Лише GitHub HTTPS без портів та кредів |
| Degenerate severity UNKNOWN | NaN-вхід не виглядає як OK |
| Corrupt matrix preserved | Битий файл у карантин з міткою |

---
## Як запустити тести

```bash
dotnet test ResourceCalculator.Tests/
```

Або з покриттям:
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov
```
