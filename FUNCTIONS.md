# FUNCTIONS.md — Публічні методи та API

> **Призначення**: Швидкий довідник по всіх публічних методах кожного класу.
> Використовуй цей файл коли потрібно знайти конкретний метод, зрозуміти його сигнатуру або викликати з нового місця.

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
void SyncGridsToMatrix();
SizingMatrix CopyMatrix();
void NormalizeModulePolicy();
```

| Метод | Параметри | Повертає | Опис |
|---|---|---|---|
| `Save` | — | `void` | Зберігає поточну матрицю |
| `Reset` | — | `void` | Скидає до дефолтів |
| `SyncGridsToMatrix` | — | `void` | Синхронізує UI-гріди → матриця |
| `CopyMatrix` | — | `SizingMatrix` | Глибока копія |
| `NormalizeModulePolicy` | — | `void` | Встановлює IsMandatory/IsKubernetesOnly |

---

## 5. ConfigExportService

**Файл**: `ResourceCalculator.Core/Services/ConfigExportService.cs`

```csharp
byte[] ExportExcel(ProjectConfig config, ResourceRequirement req, List<EnvironmentReport> envReports);
byte[] ExportPdf(ProjectConfig config, ResourceRequirement req, List<EnvironmentReport> envReports);
```

| Метод | Параметри | Повертає | Опис |
|---|---|---|---|
| `ExportExcel` | `config, req, envReports` | `byte[]` | Excel .xlsx (EPPlus) |
| `ExportPdf` | `config, req, envReports` | `byte[]` | PDF (QuestPDF, A4 landscape) |

---

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
bool Verify(string password);
void SetPassword(string newPassword);
string GetPasswordHint();
void EnsureInitialized();
```

| Метод | Параметри | Повертає | Опис |
|---|---|---|---|
| `Verify` | `string password` | `bool` | Перевірка пароля (SHA-256 + salt) |
| `SetPassword` | `string newPassword` | `void` | Встановлення нового пароля |
| `GetPasswordHint` | — | `string` | Контакти розробника |
| `EnsureInitialized` | — | `void` | Створює settings.json з дефолтним паролем |

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
| `CheckForUpdateAsync` | — | `Task<UpdateCheckResult>` | GitHub API: є нова версія? |

**UpdateCheckResult**: `{ Status: NoUpdate/UpdateAvailable/Failed, Update?: { Version, DownloadUrl } }`

---

## 10. SelfUpdateService

**Файл**: `ResourceCalculator.Core/Services/SelfUpdateService.cs`

```csharp
event DownloadProgressHandler? Progress;
string? DownloadUrl { get; set; }
Task<SelfUpdateResult> UpdateAsync(CancellationToken cancellationToken = default);
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
byte[] ExportExcel(ProjectConfig config, ResourceRequirement req, List<EnvironmentReport> envReports);
byte[] ExportPdf(ProjectConfig config, ResourceRequirement req, List<EnvironmentReport> envReports);
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
| `ResetMatrixCommand` | Скидання до дефолтів |

### Ключові методи
| Метод | Опис |
|---|---|
| `EnsureUnlocked()` | Перевірка пароля перед редагуванням |
| `LoadMatrixGrids()` | Завантаження даних з матриці в гріди |
| `SyncGridsToMatrix()` | Синхронізація грідів → матриця |

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

## Шпаргалка: якщо потрібно...

| Завдання | Метод | Файл |
|---|---|---|
| Порахувати ресурси | `SizingEngine.Calculate()` | IMPLEMENTATION.md §1 |
| Зберегти матрицю | `MatrixManager.Save()` | IMPLEMENTATION.md §3 |
| Експорт Excel | `ConfigExportService.ExportExcel()` | IMPLEMENTATION.md §4 |
| Експорт PDF | `ConfigExportService.ExportPdf()` | IMPLEMENTATION.md §4 |
| Валідувати ресурси | `ValidationEngine.Validate()` | IMPLEMENTATION.md §5 |
| Побудувати середовища | `EnvironmentBuilder.Build()` | IMPLEMENTATION.md §6 |
| Перевірити пароль | `AccessService.Verify()` | IMPLEMENTATION.md §7 |
| Додати в історію | `CalculationHistoryService.SaveToHistory()` | IMPLEMENTATION.md §8 |
| Порахувати репліки | `ReplicaMath.Resolve()` | DATA-MODELS.md §16 |
| Рекомендація дисків | `DiskAdvisor.Build()` | IMPLEMENTATION.md §12 |
