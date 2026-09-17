# FUNCTIONS.md — Публічні методи та API

> **Призначення**: Швидкий довідник по всіх публічних методах кожного класу.
> Використовуй цей файл коли потрібно знайти конкретний метод, зрозуміти його сигнатуру або викликати з нового місця.

<!-- AUTO:stamp -->Verified: 2026-09-15, commit `66d7251` (scripts/Update-Docs.ps1)<!-- /AUTO -->

---

## Зміст

1. [ISizingEngine](#1-isizingengine)
2. [IValidationEngine](#2-ivalidationengine)
3. [IDataService](#3-idataservice)
4. [MatrixManager](#4-matrixmanager)
5. [ConfigExportService](#5-configexportservice)
6. [EnvironmentBuilder](#6-environmentbuilder)
7. [AccessService](#7-accessservice)
8. [CalculationHistoryService](#8-calculationhistoryservice)
9. [UpdateCheckService](#9-updatecheckservice)
10. [SelfUpdateService](#10-selfupdateservice)
11. [ILocalizationService](#11-ilocalizationservice)
12. [DiskAdvisor](#12-diskadvisor)
13. [ResultsPresenter](#13-resultspresenter)
14. [MainViewModel](#14-mainviewmodel)
15. [MatrixViewModel](#15-matrixviewmodel)
16. [ReplicaMath](#16-replicamath)
17. [ProjectModule](#17-projectmodule)
18. [ResourceRequirement](#18-resourcerequirement)
19. [InfrastructureNode](#19-infrastructurenode)
20. [DocumentRequirements](#20-documentrequirements)
21. [DialogService (Avalonia)](#21-dialogservice-avalonia)

---

## 1. ISizingEngine

**Файл**: `ResourceCalculator.Core/Interfaces/ISizingEngine.cs`
**Реалізація**: `SizingEngine`

```csharp
IReadOnlyList<ProjectModule> Modules { get; }
void SetModules(List<ProjectModule> modules);
void ReloadModules();
ResourceRequirement Calculate(ProjectConfig config);
```

| Метод | Параметри | Повертає | Опис |
|---|---|---|---|
| `Calculate` | `ProjectConfig config` | `ResourceRequirement` | Головний розрахунок. Вибирає K8s/Windows/Hybrid |
| `SetModules` | `List<ProjectModule>` | `void` | Встановлює модулі для розрахунку |
| `ReloadModules` | — | `void` | Перезавантажує модулі з матриці |

---

## 2. IValidationEngine

**Файл**: `ResourceCalculator.Core/Interfaces/IValidationEngine.cs`
**Реалізація**: `ValidationEngine`

```csharp
List<ValidationResult> CompareProfiles(ResourceRequirement profile1, ResourceRequirement profile2);
List<ValidationResult> Validate(ResourceRequirement required, ResourceRequirement allocated);
List<ValidationResult> ValidateProject(ProjectConfig config, ResourceRequirement calculated, List<InfrastructureNode> actualResources);
```

| Метод | Параметри | Повертає | Опис |
|---|---|---|---|
| `Validate` | `required, allocated` | `List<ValidationResult>` | Порівнює потрібні vs виділені ресурси |
| `ValidateProject` | `config, calculated, actualResources` | `List<ValidationResult>` | Валідація розрахунку проти вузлів |
| `CompareProfiles` | `profile1, profile2` | `List<ValidationResult>` | Порівняння двох профілів |

---

## 3. IDataService

**Файл**: `ResourceCalculator.Core/Interfaces/IDataService.cs`
**Реалізація**: `DataService`

```csharp
void SaveMatrix(SizingMatrix matrix);
SizingMatrix LoadMatrix();
void ClearMatrix();
```

| Метод | Параметри | Повертає | Опис |
|---|---|---|---|
| `SaveMatrix` | `SizingMatrix` | `void` | Атомарний запис у matrix.json |
| `LoadMatrix` | — | `SizingMatrix` | Завантаження (або створення нової) |
| `ClearMatrix` | — | `void` | Видалення matrix.json |

---

## 4. MatrixManager

**Файл**: `ResourceCalculator.Core/Services/MatrixManager.cs`

```csharp
SizingMatrix Matrix { get; }
void Save();
void Reset();
List<string> SyncGridsToMatrix(...); // порожньо = застосовано, інакше помилки для UI
SizingMatrix CopyMatrix();
void NormalizeModulePolicy();
```

| Метод | Параметри | Повертає | Опис |
|---|---|---|---|
| `Save` | — | `void` | Зберігає поточну матрицю |
| `Reset` | — | `void` | Скидає до дефолтів |
| `SyncGridsToMatrix` | — | `List<string>` | Синхронізує UI-гріди → матриця |
| `CopyMatrix` | — | `SizingMatrix` | Глибока копія |
| `NormalizeModulePolicy` | — | `void` | Встановлює IsMandatory/IsKubernetesOnly |

---

## 4b. MatrixValidator

**Файл**: `ResourceCalculator.Core/Services/MatrixValidator.cs`

```csharp
static List<string> Validate(SizingMatrix m); // порожньо = придатна
```

Пост-завантажувальна перевірка matrix.json: форма дат, скінченність і межі чисел, Min<=Max, відсутність перетинів діапазонів, PageFileRounding != 0, капи списків. Використовують `DataService.LoadMatrix` (невалідне — у карантин) і `MatrixManager.SyncGridsToMatrix` (невалідне не потрапляє у движок).

---
## 5. ConfigExportService

**Файл**: `ResourceCalculator.Core/Services/ConfigExportService.cs` — фасад. Сама генерація
розведена по трьох файлах, бо один клас на ~1000 рядків тримав і QuestPDF-, і EPPlus-код,
і правка одного звіту змушувала читати обидва:

| Файл | Роль |
|---|---|
| `ConfigExportService.cs` | публічний фасад (`ExportPdf`/`ExportExcel`/`Xl`/`NodeRole`), делегує білдерам |
| `PdfReportBuilder.cs` | PDF (QuestPDF, A4 landscape), палітра `Pdf*`, `ComposePdf*` |
| `ExcelReportBuilder.cs` | XLSX (EPPlus), `Build*Sheet`, `StyleTable`, `WriteHeader` |
| `ReportCommon.cs` | спільні підписи й назви: `DbName`, `DeployName`, `ReportTitle`, `PodDistribution`, `Xl`, `NodeRole` |

```csharp
byte[] ExportExcel(ResourceRequirement req, ProjectConfig config, List<EnvironmentReport>? envReports = ...);
byte[] ExportPdf(ResourceRequirement req, ProjectConfig config, List<EnvironmentReport>? envReports = ...);
```

| Метод | Параметри | Повертає | Опис |
|---|---|---|---|
| `ExportExcel` | `req, config, envReports` | `byte[]` | Excel .xlsx (EPPlus); заголовки інфраструктури включають к-сть користувачів |
| `ExportPdf` | `req, config, envReports` | `byte[]` | PDF (QuestPDF, A4 landscape) |

> ⚠️ Тексти звітів (заголовки таблиць, глосарій, примітки) — **завжди українською**,
> незалежно від мови UI: це клієнтські документи для українських замовників.
> Локалізація звітів у скоуп не входить; якщо знадобиться — це окрема задача,
> а не побічний ефект перемикача мови.

---


> Excel-санітизація: `public static string Xl(string? s)` — значення з початковими `= + - @` отримують префікс-апостроф проти formula injection. Застосовано до всіх матричних/конфігових рядків у звіті.
## 6. EnvironmentBuilder

**Файл**: `ResourceCalculator.Core/Services/EnvironmentBuilder.cs`

```csharp
List<EnvironmentReport> Build(ProjectConfig config, EnvironmentSettings envSettings, List<EnvModuleCount> moduleCounts);
```

| Метод | Параметри | Повертає | Oпис |
|---|---|---|---|
| `Build` | `config, envSettings, moduleCounts` | `List<EnvironmentReport>` | PROD + опціональні DEV/TEST/PreProd |

---

## 7. AccessService

**Файл**: `ResourceCalculator.Core/Services/AccessService.cs`

```csharp
bool IsPasswordSet { get; }
bool Verify(string password); // PBKDF2, fail closed, тротлінг
bool Verify(SecureString password); // байтовий шлях без string-копій
void SetPassword(string newPassword); // мінімум 12 символів, кидає виняток при помилці
TimeSpan LockoutRemaining { get; } // залишок блокування для UI
string GetPasswordHint();
void EnsureInitialized();
```

| Метод | Параметри | Повертає | Опис |
|---|---|---|---|
| `IsPasswordSet` | — | `bool` | Чи існує settings.json |
| `Verify` | `string password` | `bool` | Перевірка пароля (SHA-256 + salt) |
| `SetPassword` | `string newPassword` | `void` | Встановлення нового пароля |
| `GetPasswordHint` | — | `string` | Контакти розробника |
| `EnsureInitialized` | — | `void` | Створює settings.json з дефолтним паролем |

Асинхронне розблокування матриці — на `MatrixViewModel.EnsureUnlockedAsync()` (§15), не тут.

---

## 8. CalculationHistoryService

**Файл**: `ResourceCalculator.Core/Services/CalculationHistoryService.cs`

```csharp
List<CalculationHistoryItem> LoadHistory();
void SaveToHistory(ProjectConfig config, ResourceRequirement req);
```

| Метод | Параметри | Повертає | Опис |
|---|---|---|---|
| `LoadHistory` | — | `List<CalculationHistoryItem>` | Завантаження історії |
| `SaveToHistory` | `config, req` | `void` | Додавання запису (макс. 20) |

---

## 9. UpdateCheckService

**Файл**: `ResourceCalculator.Core/Services/UpdateCheckService.cs`

```csharp
Task<UpdateCheckResult> CheckForUpdateAsync();
```

| Метод | Параметри | Повертає | Опис |
|---|---|---|---|
| `CheckForUpdateAsync` | — | `Task<UpdateCheckResult>` | GitHub API `/releases/latest`: є нова версія? 3 ретраї з backoff, таймаут 15с, помилки → `Failed` (не кидає) |

**UpdateCheckResult**: `{ Status: NoUpdate/UpdateAvailable/Failed, Update?: UpdateInfo }`

**UpdateInfo**: `record UpdateInfo(string Version, string DownloadUrl, string? ReleaseNotes = null, long SizeBytes = 0, string? Sha256 = null)` — прямий `browser_download_url` exe-ассета `ITE.ResourceCalculator.exe`, нотатки з `body`, розмір з `size`, `Sha256` — з поля `digest` того самого ассета (`sha256:<64 hex>`, інші форми відкидаються). `Sha256` резолвиться тут, а не в `SelfUpdateService`: раніше той робив другий запит до `/releases/latest` уже після завантаження — зайвий удар по rate-limit (60/год на IP) і вікно, в яке «latest» міг стати наступним релізом, через що перевірка падала на коректному файлі. Жодних переходів у браузер — оновлення повністю всередині програми (`App.CheckForUpdatesAsync` → `UpdateAvailableDialog` → `StartUpdateAsync`).

---

## 10. SelfUpdateService

**Файл**: `ResourceCalculator.Core/Services/SelfUpdateService.cs`

```csharp
event DownloadProgressHandler? Progress;
Task<SelfUpdateResult> UpdateAsync(string downloadUrl, CancellationToken cancellationToken = default); // URL параметром, не mutable-властивість
static bool IsAllowedDownloadUrl(string? url, out string error); // allowlist: лише GitHub HTTPS
```

| Метод | Параметри | Повертає | Опис |
|---|---|---|---|
| `UpdateAsync` | `CancellationToken` | `Task<SelfUpdateResult>` | Завантаження + SHA256 + заміна |

**SelfUpdateResult**: `{ Status: InProgress/Completed/Failed, Error?: string }`

---

## 11. ILocalizationService

**Файл**: `ResourceCalculator.Core/Interfaces/ILocalizationService.cs`

```csharp
string CurrentLang { get; }
string Flag { get; }
string LangName { get; }
string this[string key] { get; }
string Get(string key);
void LoadLanguage(string lang);
```

| Метод/Властивість | Повертає | Опис |
|---|---|---|
| `this[key]` | `string` | Доступ до рядка за ключем |
| `Get(key)` | `string` | Аналог індексатора |
| `LoadLanguage(lang)` | `void` | `"uk"` або `"en"` |
| `CurrentLang` | `string` | Поточна мова |
| `Flag` | `string` | Емодзі прапора |
| `LangName` | `string` | Назва мови |

---

## 12. DiskAdvisor

**Файл**: `ResourceCalculator.Core/Services/DiskAdvisor.cs` (статичний клас)

```csharp
static string Build(InfrastructureNode node);
```

| Метод | Параметри | Повертає | Опис |
|---|---|---|---|
| `Build` | `InfrastructureNode` | `string` | Текстова рекомендація дисків |

---

## 13. ResultsPresenter

**Файл**: `ResourceCalculator.Core/Services/ResultsPresenter.cs`

```csharp
List<ValidationResult> CompareProfiles(ResourceRequirement p1, ResourceRequirement p2);
List<ValidationResult> Validate(ResourceRequirement required, ResourceRequirement allocated);
List<ValidationResult> ValidateProject(ProjectConfig config, ResourceRequirement calculated, List<InfrastructureNode> actual);
byte[] ExportExcel(ResourceRequirement req, ProjectConfig config, List<EnvironmentReport>? envReports = ...);
byte[] ExportPdf(ResourceRequirement req, ProjectConfig config, List<EnvironmentReport>? envReports = ...);
```

Делегує відповідним сервісам (фасад).

---

## 14. MainViewModel

**Файл**: `ResourceCalculator.Core/ViewModels/MainViewModel.cs`

### Ключові команди
| Команда | Тип | Опис |
|---|---|---|
| `CalculateCommand` | `AsyncRelayCommand` | Розрахунок ресурсів |
| `ExportExcelCommand` | `AsyncRelayCommand` | Експорт в Excel |
| `ExportPdfCommand` | `AsyncRelayCommand` | Експорт в PDF |
| `ThemeSwitchCommand` | `RelayCommand` | Перемикання теми |
| `LangSwitchCommand` | `RelayCommand` | Перемикання мови |
| `RecallHistoryCommand` | `RelayCommand` | Відновлення з історії |

### Ключові методи
| Метод | Опис |
|---|---|
| `Calculate()` | Будує ProjectConfig → EnvironmentBuilder.Build() → оновлює UI |
| `ExportExcel()` | Формує config → ResultsPresenter.ExportExcel() → збереження файлу |
| `ExportPdf()` | Формує config → ResultsPresenter.ExportPdf() → збереження файлу |
| `BuildSummary()` | Текстовий підсумок для UI |
| `BuildDiskRecommendations()` | Рекомендації по дисках для кожного вузла |

---

## 15. MatrixViewModel

**Файл**: `ResourceCalculator.Core/ViewModels/MatrixViewModel.cs`

### Ключові команди
| Команда | Опис |
|---|---|
| `SaveMatrixCommand` | Збереження матриці (з перевіркою пароля) |
| `RecalculateMatrixCommand` | Перерахунок після зміни матриці |
| `ResetMatrixCommand` | Скидання до дефолтів (без пароля) |

> Команди додавання рядка немає свідомо: модель матриці має рівно вісім слотів вузлів
> (`NodeSlot`), тож рядок без слота нікуди не зберігався б. Колишні `AddRowCommand`/
> `AddRowAsync` не були прив'язані ні до одного елемента XAML — мертвий код, що виглядав
> як робоча функція.

### Ключові методи
| Метод | Опис |
|---|---|
| `EnsureUnlockedAsync()` | Асинхронна перевірка пароля перед редагуванням (`Task<bool>`); викликається Save/Recalculate |
| `SyncGridsToMatrix()` | Гріди → матриця; повертає список помилок валідації (порожній = застосовано) |
| `LoadMatrixGrids()` | Завантаження даних з матриці в гріди |
| `SyncGridsToMatrix()` | Синхронізація грідів → матриця |

Захист комірки DataGrid — у code-behind `Views/MatrixTabControl.xaml.cs` (`BeginningEdit` + `Dispatcher.UIThread.Post` + повторний `BeginEdit` після розблокування).

---

## 16. ReplicaMath

**Файл**: `ResourceCalculator.Core/Models/ProjectModule.cs` (статичний клас)

```csharp
static int Resolve(ReplicaFormula formula, int fixedReplicas, int userCount, int auxUsers = -1);
```

| Formula | Результат |
|---|---|
| `Fixed` | `fixedReplicas` |
| `Per25Users` | `Ceiling(userCount / 25)` |
| `Per50Users` | `Ceiling(userCount / 50)` |
| `Per100Users` | `Ceiling(userCount / 100)` |
| `Per1000Users` | `Ceiling(userCount / 1000)` |
| `OnePlusPer100` | `1 + Int(userCount / 100)` |
| `Per100Plus1000` | `1 + Int(userCount/100) + Int(auxUsers/1000)` |
| `Per50Plus500` | `1 + Int(userCount/50) + Int(auxUsers/500)` |
| `HrPortalGraphqlLoadTest` | Емпірична: 500→3, 1000→8, далі +1/125 |
| `HrPortalSmartIdLoadTest` | Емпірична: 500→2, 1000→14, далі +1/75 |
| `HrPortalRobotLoadTest` | ≤1000→1, далі +1/1000 |
| `LmsGraphqlLoadTest` | Таблична: 50→1, 100→2, 150→3, 200→5, 250→7, далі +2/50 |

---

## 17. ProjectModule

**Файл**: `ResourceCalculator.Core/Models/ProjectModule.cs`

```csharp
int EffectiveUsers(int projectUsers, bool cap = false);
(double cpu, double ram) CalculateReplicas(int userCount, int auxUsers = -1);
ProjectModule Clone();
```

| Метод | Параметри | Повертає | Опис |
|---|---|---|---|
| `EffectiveUsers` | `projectUsers, cap` | `int` | Ефективна к-сть користувачів модуля |
| `CalculateReplicas` | `userCount, auxUsers` | `(double, double)` | Сумарний CPU/RAM модуля |
| `Clone` | — | `ProjectModule` | Глибока копія |

---

## 18. ResourceRequirement

**Файл**: `ResourceCalculator.Core/Models/ResourceRequirement.cs`

```csharp
ResourceRequirement DeepClone();
string Summary();
```

| Метод | Повертає | Опис |
|---|---|---|
| `DeepClone` | `ResourceRequirement` | Глибока копія (для середовищ) |
| `Summary` | `string` | Текстовий підсумок |

---

## 19. InfrastructureNode

**Файл**: `ResourceCalculator.Core/Models/InfrastructureNode.cs`

```csharp
int DiskPerNodeGb { get; }    // Сума всіх дисків + pagefile
int TotalStorageGb { get; }   // DiskPerNodeGb × NodeCount
InfrastructureNode Clone();
```

---

## 20. DocumentRequirements

**Файл**: `ResourceCalculator.Core/Data/DocumentRequirements.cs`

```csharp
static List<DocComparisonItem> Compare(ProjectConfig config, ResourceRequirement calculated, SizingMatrix matrix);
```

| Метод | Параметри | Повертає | Опис |
|---|---|---|---|
| `Compare` | `config, calculated, matrix` | `List<DocComparisonItem>` | Порівняння з еталоном D-AD-ADM-E |

---

## 21. DialogService (Avalonia)

**Файл**: `ResourceCalculator/Dialogs/DialogService.cs` (реєструється як `IDialogService` + `IFileSaveService`)
**Допоміжний**: `ResourceCalculator/Dialogs/ThemeService.cs` — міст `IThemeService` → static `Themes.ThemeService`

```csharp
Task<bool> ConfirmAsync(string message, string title);
Task InfoAsync(string message, string title);
Task ErrorAsync(string message, string title);
Task<bool> ShowPasswordDialogAsync();
Task<string?> PickSavePathAsync(string defaultFileName, string filterDescription, string extension);
```

Вікна оновлення — `Views/UpdateAvailableDialog` (версії, розмір, «Що нового», Пізніше/Оновити зараз) і `Views/UpdateProgressDialog` (`SetProgress/SetCompleted/SetError`, `RetryRequested`, токен скасування). Оркестрація — `App.CheckForUpdatesAsync` / `App.StartUpdateAsync` (див. ARCHITECTURE.md §6).

---

## Шпаргалка: якщо потрібно...

| Завдання | Метод | Файл |
|---|---|---|
| Порахувати ресурси | `SizingEngine.Calculate()` | IMPLEMENTATION.md §1 |
| Зберегти матрицю | `MatrixManager.Save()` | IMPLEMENTATION.md §3 |
| Експорт Excel | `ConfigExportService.ExportExcel(req, config, ...)` | IMPLEMENTATION.md §4 |
| Експорт PDF | `ConfigExportService.ExportPdf(req, config, ...)` | IMPLEMENTATION.md §4 |
| Валідувати ресурси | `ValidationEngine.Validate()` | IMPLEMENTATION.md §5 |
| Побудувати середовища | `EnvironmentBuilder.Build()` | IMPLEMENTATION.md §6 |
| Перевірити пароль | `AccessService.Verify()` | IMPLEMENTATION.md §7 |
| Додати в історію | `CalculationHistoryService.SaveToHistory()` | IMPLEMENTATION.md §8 |
| Порахувати репліки | `ReplicaMath.Resolve()` | DATA-MODELS.md §16 |
| Рекомендація дисків | `DiskAdvisor.Build()` | IMPLEMENTATION.md §12 |
