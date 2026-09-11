# DATA-MODELS.md — Моделі даних проєкту

> **Призначення**: Детальний опис кожної моделі даних, властивостей, типів та зв'язків.
> Використовуй цей файл для швидкого розуміння структур даних без перечитування коду.

---

## Зміст

1. [Enums](#1-enums)
2. [UserLoadRange](#2-userloadrange)
3. [ModuleComponent](#3-modulecomponent)
4. [ProjectModule](#4-projectmodule)
5. [ProjectConfig](#5-projectconfig)
6. [InfrastructureNode](#6-infrastructurenode)
7. [ServiceComponent](#7-servicecomponent)
8. [ResourceRequirement](#8-resourcerequirement)
9. [ValidationResult](#9-validationresult)
10. [EngineSettings](#10-enginesettings)
11. [EnvironmentModels](#11-environmentmodels)
12. [CalculationHistoryItem](#12-calculationhistoryitem)
13. [ComponentDisplayName](#13-componentdisplayname)
14. [SizingMatrix](#14-sizingmatrix) (Data/)
15. [DocumentRequirements](#15-documentrequirements) (Data/)
16. [ReplicaMath](#16-replicamath)

---

## 1. Enums

### `DeploymentType`
Тип розгортання інфраструктури.
| Значення | Опис |
|---|---|
| `Kubernetes` | K8s-кластер: поди на worker-вузлах, master + SQL |
| `Windows` | Чисті VM: SQL + App Servers + Web Servers |
| `Hybrid` | Комбінований: K8s для ForceBPM/сервісів + Windows для App/Web/DB |

### `LoadProfile`
Профіль навантаження (поки що лише один).
| Значення | Опис |
|---|---|
| `Performance` | Продакшн-профіль з урахуванням PerfCPU/PerfRAM |

### `DatabaseType`
Тип СУБД.
| Значення | Опис |
|---|---|
| `MsSql` | Microsoft SQL Server (основний, за документом D-AD-ADM-E) |
| `PostgreSQL` | PostgreSQL (оцінкові діапазони) |
| `Oracle` | Oracle 19c (оцінкові діапазони) |

### `DeployEnvironment`
Середовище розгортання.
| Значення | Опис | Редакція СУБД |
|---|---|---|
| `Prod` | Продакшн | Standard/Enterprise |
| `Dev` | Розробка | Developer Edition |
| `Test` | Тестування | Developer Edition |
| `PredProd` | Перед-продакшн | Developer Edition |

### `ReplicaFormula`
Формула розрахунку кількості реплік подів K8s. Детальний опис у [Розділі 16](#16-replicamath).
| Значення | Формула |
|---|---|
| `Fixed` | Фіксована кількість (FixedReplicas) |
| `Per25Users` | `Ceiling(users / 25)` |
| `Per50Users` | `Ceiling(users / 50)` |
| `Per100Users` | `Ceiling(users / 100)` |
| `Per1000Users` | `Ceiling(users / 1000)` |
| `OnePlusPer100` | `1 + Int(users / 100)` |
| `Per100Plus1000` | `1 + Int(users/100) + Int(auxUsers/1000)` (ROBOT) |
| `Per50Plus500` | `1 + Int(users/50) + Int(auxUsers/500)` (WS) |
| `HrPortalGraphqlLoadTest` | Емпірична матриця з тестів HR Portal |
| `HrPortalSmartIdLoadTest` | Емпірична матриця SmartID з тестів |
| `HrPortalRobotLoadTest` | Емпірична матриця ROBOT з тестів |
| `LmsGraphqlLoadTest` | Точна таблиця з тесту LMS-GraphQL |

---

## 2. UserLoadRange

**Файл**: `ResourceCalculator.Core/Models/UserLoadRange.cs`
**Призначення**: Один рядок таблиці діапазонів користувачів (матриця розмірування).

| Властивість | Тип | Опис |
|---|---|---|
| `MinUsers` | `int` | Мінімальна кількість користувачів діапазону |
| `MaxUsers` | `int` | Максимальна кількість користувачів діапазону |
| `Cpu` | `double` | Кількість CPU (ядер) для вузла |
| `RamMin` | `double` | Мінімальний обсяг RAM (GB) |
| `RamRec` | `double` | Рекомендований обсяг RAM (GB) |
| `Iops` | `int` | IOPS (вхід/вихід операцій на секунду) |
| `Latency` | `double` | Латентність диска (мс) |
| `InstanceCount` | `int` | Кількість VM/інстансів (для App/Web серверів) |
| `Ghz` | `double` | Частота процесора (ГГц) |
| `ThroughputMiBs` | `int` | Пропускна здатність диска (МіБ/с) |
| `IopsProfile` | `string` | Профіль читання/запису, напр. `"50r/50w"` |

**Методи**:
- `Clone()` — повертає `MemberwiseClone()` (поверхнева копія).

**Де використовується**: Колекції в `SizingMatrix` (MsSqlRanges, AppServerRanges, WebServerRanges, PostgresRanges, OracleRanges).

---

## 3. ModuleComponent

**Файл**: `ResourceCalculator.Core/Models/ProjectModule.cs`
**Призначення**: Окремий компонент модуля (наприклад, "AS-Local SQL" всередині модуля "App Server").

| Властивість | Тип | Опис |
|---|---|---|
| `Name` | `string` | Назва компонента (напр. `"AS (App Server)"`) |
| `Cpu` | `double` | CPU на 1 репліку |
| `RamGb` | `double` | RAM (GB) на 1 репліку |
| `PerfCpu` | `double` | CPU для профілю Performance (>0 замінює базовий) |
| `PerfRamGb` | `double` | RAM для профілю Performance (>0 замінює базовий) |
| `FixedReplicas` | `int` | Фіксована кількість реплік (для `Formula = Fixed`) |
| `Formula` | `ReplicaFormula` | Формула розрахунку реплік |
| `HasLocalSql` | `bool` | Чи має компонент локальну БД |
| `HasRedis` | `bool` | Чи має компонент Redis |
| `Notes` | `string` | Примітки |

**Методи**:
- `Clone()` — глибока копія.

---

## 4. ProjectModule

**Файл**: `ResourceCalculator.Core/Models/ProjectModule.cs`
**Призначення**: Модуль системи (App Server, ROBOT, Web, ForceBPM, LMS, HR Portal). Реалізує `INotifyPropertyChanged`.

| Властивість | Тип | Опис |
|---|---|---|
| `Name` | `string` | Назва модуля |
| `Description` | `string` | Опис модуля |
| `IsEnabled` | `bool` | Увімкнений/вимкнений (SPCs через PropertyChanged) |
| `IsKubernetesOnly` | `bool` | Тільки для K8s (ForceBPM) |
| `IsMandatory` | `bool` | Обов'язковий (App Server, ROBOT, Web) — завжди увімкнений |
| `HasOwnUserCount` | `bool` | Чи має власну кількість користувачів (LMS/HR Portal — true) |
| `UserCount` | `int` | Власна кількість користувачів (0 = загальна) |
| `Components` | `List<ModuleComponent>` | Список компонентів модуля |

**Методи**:
- `EffectiveUsers(projectUsers, cap)` — ефективна кількість користувачів (власна або загальна). `cap=true` обмежує загальною кількістю.
- `CalculateReplicas(userCount, auxUsers)` — рахує сумарний CPU/RAM модуля з урахуванням реплік усіх компонентів.
- `Clone()` — глибока копія.

**Модулі за замовчуванням** (з `SizingMatrix`):
| Модуль | IsMandatory | IsKubernetesOnly | HasOwnUserCount |
|---|---|---|---|
| App Server | ✅ | ❌ | ❌ |
| ROBOT | ✅ | ❌ | ❌ |
| Web | ✅ | ❌ | ❌ |
| ForceBPM | ❌ | ✅ | ❌ |
| LMS | ❌ | ❌ | ✅ |
| HR Portal | ❌ | ❌ | ✅ |

---

## 5. ProjectConfig

**Файл**: `ResourceCalculator.Core/Models/ProjectConfig.cs`
**Призначення**: Вхідна конфігурація проєкту для розрахунку.

| Властивість | Тип | Опис |
|---|---|---|
| `ProjectName` | `string` | Назва проєкту |
| `UserCount` | `int` | Кількість користувачів (ліцензій) |
| `DeploymentType` | `DeploymentType` | Тип розгортання (за замовч. Kubernetes) |
| `LoadProfile` | `LoadProfile` | Профіль навантаження (за замовч. Performance) |
| `DatabaseType` | `DatabaseType` | Тип СУБД (за замовч. MsSql) |
| `SelectedModules` | `List<string>` | Увімкнені модулі |
| `IncludeReportingServer` | `bool` | Опціональний сервер звітів |
| `IncludeSqlFailover` | `bool` | Опціональний failover-кластер БД |
| `IncludeHaProxy` | `bool` | Опціональний балансувальник HAProxy |
| `Environment` | `DeployEnvironment` | Середовище (за замовч. Prod) |
| `DbSizeGb` | `int` | Обсяг даних БД (ГБ). 0 = фіксовані з матриці |
| `ContentDbSizeGb` | `int` | Обсяг Content (ГБ). 0 = фіксоване з матриці |
| `IncludeComponentsInReport` | `bool` | Включати компоненти у звіт (за замовч. true) |

---

## 6. InfrastructureNode

**Файл**: `ResourceCalculator.Core/Models/InfrastructureNode.cs`
**Призначення**: Модель сервера/VM (вузла інфраструктури).

| Властивість | Тип | Опис |
|---|---|---|
| `Name` | `string` | Назва вузла (напр. `"K8s SQL"`, `"K8s Master"`) |
| `Os` | `string` | Операційна система |
| `Cpu` | `double` | Кількість CPU (ядер) |
| `Ghz` | `double` | Частота процесора (ГГц). 0 = не показувати |
| `RamGb` | `double` | Обсяг RAM (GB) |
| `NodeCount` | `int` | Кількість вузлів цього типу |
| `StorageType` / `StorageGb` | `string`/`int` | Тип та обсяг диска 1 (OS) |
| `StorageType2` / `StorageGb2` | `string`/`int` | Диск 2 (Logs+TempDB) |
| `StorageType3` / `StorageGb3` | `string`/`int` | Диск 3 (MainData) |
| `StorageType4` / `StorageGb4` | `string`/`int` | Диск 4 (Content) |
| `MinVersion` | `double` | Мінімальна версія СУБД |
| `Iops` | `int` | IOPS |
| `IopsProfile` | `string` | Профіль читання/запису |
| `ThroughputMiBs` | `int` | Пропускна здатність (МіБ/с) |
| `Latency` | `double` | Латентність (мс) |
| `PageFileGb` | `int` | Файл підкачки (GB) |
| `PageFileType` | `string` | Тип файлу підкачки |
| `DbVersion` | `string` | Версія/редакція СУБД (напр. `"MS SQL Server 2022 Standard"`) |
| `DiskSplitNotApplicable` | `bool` | Прапорець "не застосовно" для розбиття дисків |
| `PageFileNotApplicable` | `bool` | Прапорець "не застосовно" для pagefile |
| `IopsNotApplicable` | `bool` | Прапорець "не застосовно" для IOPS |
| `Notes` | `string` | Примітки |

**Обчислювані властивості**:
- `DiskPerNodeGb` — сума всіх дисків одного вузла: `StorageGb + StorageGb2 + StorageGb3 + StorageGb4 + PageFileGb`
- `TotalStorageGb` — сумарний обсяг з урахуванням кількості: `DiskPerNodeGb × NodeCount`

**Розбиття дисків за типом вузла**:
| Вузол | Диск 1 (OS) | Диск 2 | Диск 3 | Диск 4 | PageFile |
|---|---|---|---|---|---|
| SQL Server | OS | Logs+TempDB | MainData | Content | ❌ |
| App/Web Server | OS | — | — | — | ✅ `CEILING(RAM×4, 10)` |
| K8s Master/Worker | OS | — | — | — | ❌ |
| Reporting Server | OS | — | — | — | ❌ |

---

## 7. ServiceComponent

**Файл**: `ResourceCalculator.Core/Models/ServiceComponent.cs`
**Призначення**: Компонент/под K8s у результатах розрахунку.

| Властивість | Тип | Опис |
|---|---|---|
| `Name` | `string` | Назва компонента |
| `Cpu` | `double` | Загальний CPU (на всі репліки) |
| `RamGb` | `double` | Загальна RAM (на всі репліки, GB) |
| `PerfCpu` | `double` | CPU для Performance-профілю |
| `PerfRamGb` | `double` | RAM для Performance-профілю |
| `CpuPerReplica` | `double` | CPU на ОДНУ репліку |
| `RamPerReplicaGb` | `double` | RAM на ОДНУ репліку (GB) |
| `Replicas` | `int` | Розрахована кількість реплік |
| `FixedReplicas` | `int` | Фіксована кількість |
| `Instances` | `int` | Кількість інстансів |
| `HasLocalSql` | `bool` | Наявність локальної БД |
| `HasRedis` | `bool` | Наявність Redis |
| `Notes` | `string` | Примітки |
| `Category` | `string` | Категорія (App Server, Web тощо) |
| `Formula` | `ReplicaFormula` | Формула розрахунку реплік |

---

## 8. ResourceRequirement

**Файл**: `ResourceCalculator.Core/Models/ResourceRequirement.cs`
**Призначення**: Результат розрахунку ресурсів для одного середовища.

| Властивість | Тип | Опис |
|---|---|---|
| `UserCount` | `int` | Кількість користувачів |
| `DeploymentType` | `DeploymentType` | Тип розгортання |
| `LoadProfile` | `LoadProfile` | Профіль навантаження |
| `TotalCpu` | `double` | Загальний CPU фізичних вузлів |
| `TotalRamGb` | `double` | Загальна RAM фізичних вузлів (GB) |
| `TotalStorageGb` | `int` | Загальний обсяг дисків (GB) |
| `PodCpu` | `double` | Сумарний запит CPU подів K8s (для Windows = 0) |
| `PodRamGb` | `double` | Сумарний запит RAM подів K8s |
| `TotalIops` | `int` | Загальні IOPS |
| `TotalLatency` | `double` | Максимальна латентність |
| `WorkerNodeCount` | `int` | Кількість worker-вузлів |
| `MasterNodeCount` | `int` | Кількість master-вузлів |
| `Components` | `List<ServiceComponent>` | Список компонентів/подів |
| `Infrastructure` | `List<InfrastructureNode>` | Список вузлів інфраструктури |

**Методи**:
- `DeepClone()` — глибока копія (вузли клонуються поодинці, компоненти — у новий список). Використовується для похідних середовищ, щоб модифікації не зачіпали PROD.
- `Summary()` — текстовий підсумок: `[Type / Profile] Users: N \n CPU: X | RAM: Y GB | ...`

---

## 9. ValidationResult

**Файл**: `ResourceCalculator.Core/Models/ValidationResult.cs`
**Призначення**: Результат валідації окремого ресурсу.

| Властивість | Тип | Опис |
|---|---|---|
| `ResourceName` | `string` | Назва ресурсу (CPU, RAM, Storage тощо) |
| `Required` | `double` | Потрібна кількість |
| `Allocated` | `double` | Виділена кількість |
| `Unit` | `string` | Одиниця вимірювання |
| `Severity` | `string` | `"OK"` / `"Warning"` / `"Critical"` / `"Overprovisioned"` |
| `Recommendation` | `string` | Рекомендація щодо виправлення |

**Обчислювані властивості**:
- `IsCompliant` — `Allocated >= Required`
- `Delta` — `Allocated - Required`
- `DeltaPercent` — `% з різниці від Required`

**Пороги Severity** (з `ValidationEngine`):
| Умова | Severity |
|---|---|
| `DeltaPercent < -20%` | `Critical` |
| `DeltaPercent < 0` | `Warning` |
| `DeltaPercent > 50%` | `Overprovisioned` |
| Інше | `OK` |

---

## 10. EngineSettings

**Файл**: `ResourceCalculator.Core/Models/EngineSettings.cs`
**Призначення**: Редаговані константи рушія розрахунку. Зберігаються в `matrix.json`.

### SmartID (SSO)
| Властивість | За замовч. | Опис |
|---|---|---|
| `SmartIdCpuPerReplica` | `0.2` | CPU на 1 репліку SmartID |
| `SmartIdRamPerReplicaGb` | `0.5` | RAM на 1 репліку SmartID |

### HR Portal (load-test sizing)
| Властивість | За замовч. | Опис |
|---|---|---|
| `HrPortalGraphqlCpuPerReplica` | `0.5` | GraphQL CPU/репліку |
| `HrPortalGraphqlRamPerReplicaGb` | `0.5` | GraphQL RAM/репліку |
| `HrPortalSmartIdCpuPerReplica` | `1.25` | SmartID CPU/репліку |
| `HrPortalSmartIdRamPerReplicaGb` | `0.5` | SmartID RAM/репліку |
| `HrPortalRobotCpuPerReplica` | `10.58` | ROBOT CPU/репліку |
| `HrPortalRobotRamPerReplicaGb` | `2.13` | ROBOT RAM/репліку |

### Профілі IOPS
| Властивість | За замовч. | Опис |
|---|---|---|
| `DbIopsProfile` | `"50r/50w"` | Сервер БД |
| `AppServerIopsProfile` | `"30r/70w"` | Сервери додатків |
| `WebServerIopsProfile` | `"70r/30w"` | Веб-сервери |
| `K8sIopsProfile` | `"30r/70w"` | Вузли Kubernetes |

### Worker-вузол (за замовч.)
| Властивість | За замовч. | Опис |
|---|---|---|
| `DefaultWorkerCpu` | `8` | CPU |
| `DefaultWorkerRamGb` | `32` | RAM (GB) |
| `DefaultWorkerIops` | `500` | IOPS |
| `DefaultWorkerLatency` | `5` | Латентність (мс) |

### Master/etcd
| Властивість | За замовч. | Опис |
|---|---|---|
| `EtcdIops` | `1000` | IOPS |
| `EtcdIopsProfile` | `"10r/90w"` | Профіль |
| `EtcdThroughputMiBs` | `50` | Пропускна здатність (МіБ/с) |
| `EtcdLatency` | `10` | Латентність (мс) |
| `AvgBlockSizeKb` | `16` | Середній розмір I/O-блоку (КБ) |

### App/Web сервери (коли матриця не задає)
| Властивість | За замовч. | Опис |
|---|---|---|
| `AppServerThroughputMiBs` | `100` | Пропускна здатність App (МіБ/с) |
| `AppServerLatency` | `10` | Латентність App (мс) |
| `WebServerThroughputMiBs` | `80` | Пропускна здатність Web (МіБ/с) |
| `WebServerLatency` | `10` | Латентність Web (мс) |

### MS SQL Standard ліміти
| Властивість | За замовч. | Опис |
|---|---|---|
| `MsSqlStandardMaxRamGb` | `128` | Макс. RAM для Standard |
| `MsSqlStandardMaxCores` | `24` | Макс. ядра для Standard |

### Файл підкачки
| Властивість | За замовч. | Опис |
|---|---|---|
| `PageFileMultiplier` | `4` | Множник RAM × n |
| `PageFileRounding` | `10` | Округлення вгору до кратного |

---

## 11. EnvironmentModels

**Файл**: `ResourceCalculator.Core/Models/EnvironmentModels.cs`

### EnvironmentSettings
Налаштування похідних середовищ (DEV/TEST/PreProd).

| Властивість | Тип | Опис |
|---|---|---|
| `IncludeDev` | `bool` | Увімкнути DEV |
| `IncludeTest` | `bool` | Увімкнути TEST |
| `IncludePredProd` | `bool` | Увімкнути PreProd |
| `DevUserCount` | `int` | Кількість користувачів DEV (за замовч. 10) |
| `TestUserCount` | `int` | Кількість користувачів TEST (за замовч. 25) |
| `PredProdUserCount` | `int` | Кількість користувачів PreProd (за замовч. 50) |
| `DevDbSizeGb` | `int` | Обсяг БД DEV |
| `TestDbSizeGb` | `int` | Обсяг БД TEST (не менше PROD) |
| `PredProdDbSizeGb` | `int` | Обсяг БД PreProd (не менше PROD) |
| `DevContentDbSizeGb` | `int` | Обсяг Content DEV |
| `TestContentDbSizeGb` | `int` | Обсяг Content TEST |
| `PredProdContentDbSizeGb` | `int` | Обсяг Content PreProd |
| `AnyDerived` | `bool` (обчисл.) | Чи є хоч одне похідне середовище |

### EnvModuleCount
Рядок таблиці кількості користувачів модуля по середовищах.

| Властивість | Тип | Опис |
|---|---|---|
| `ModuleName` | `string` | Назва модуля |
| `DevUsers` | `int` | Кількість користувачів у DEV |
| `TestUsers` | `int` | Кількість користувачів у TEST |
| `PredProdUsers` | `int` | Кількість користувачів у PreProd |
| `HasOwnUserCount` | `bool` | Чи показувати поле кількості |
| `DevEnabled` | `bool` | Увімкнення модуля в DEV |
| `TestEnabled` | `bool` | Увімкнення модуля в TEST |
| `PredProdEnabled` | `bool` | Увімкнення модуля в PreProd |

**Методи**:
- `CountFor(env)` — кількість користувачів для середовища.
- `EnabledFor(env)` — чи увімкнений модуль у середовищі.

### EnvNodeToggle
Рядок таблиці додаткових вузлів по середовищах.

| Властивість | Тип | Опис |
|---|---|---|
| `Key` | `string` | Ідентифікатор: `"reporting"` / `"failover"` / `"haproxy"` |
| `NodeName` | `string` | Людська назва для UI |
| `DevEnabled` | `bool` | Увімкнення в DEV |
| `TestEnabled` | `bool` | Увімкнення в TEST |
| `PredProdEnabled` | `bool` | Увімкнення в PreProd |
| `IsEditable` | `bool` | Чи може користувач змінювати (завжди true для похідних) |

### EnvironmentReport
Один порахований звіт середовища.

| Властивість | Тип | Опис |
|---|---|---|
| `Environment` | `DeployEnvironment` | Середовище |
| `Name` | `string` | Людська назва |
| `UserCount` | `int` | Кількість користувачів |
| `Requirement` | `ResourceRequirement` | Результат розрахунку |
| `IsProd` | `bool` (обчисл.) | Чи це PROD |
| `ModulesInfo` | `string` | Інфо про модулі (напр. "LMS: 10 · HR Portal: 10") |
| `HasModulesInfo` | `bool` (обчисл.) | Чи є інфо |
| `Cpu` | `double` (обчисл.) | CPU з Requirement |
| `RamGb` | `double` (обчисл.) | RAM з Requirement |
| `StorageGb` | `int` (обчисл.) | Storage з Requirement |
| `Iops` | `int` (обчисл.) | IOPS з Requirement |
| `Nodes` | `int` (обчисл.) | Загальна кількість вузлів |
| `Vms` | `IEnumerable` (обчисл.) | Вузли з NodeCount > 0 |
| `Components` | `IEnumerable` (обчисл.) | Компоненти з Cpu > 0 |
| `HasComponents` | `bool` (обчисл.) | Чи є компоненти |
| `ComponentsCpu` | `double` (обчисл.) | Загальний CPU компонентів |
| `ComponentsRamGb` | `double` (обчисл.) | Загальна RAM компонентів |

---

## 12. CalculationHistoryItem

**Файл**: `ResourceCalculator.Core/Models/CalculationHistoryItem.cs`
**Призначення**: Запис в історії розрахунків.

| Властивість | Тип | Опис |
|---|---|---|
| `Timestamp` | `DateTime` | Час розрахунку |
| `Config` | `ProjectConfig` | Конфігурація, з якою рахували |
| `TotalCpu` | `double` | Підсумковий CPU |
| `TotalRamGb` | `double` | Підсумкова RAM (GB) |
| `TotalStorageGb` | `double` | Підсумковий обсяг дисків (GB) |
| `TotalIops` | `double` | Підсумкові IOPS |
| `TotalNodes` | `int` | Загальна кількість вузлів |

**Методи**:
- `DisplayText()` — рядок для UI: `"500 users | Kubernetes | 120.5 CPU / 256.0 GB | 11.09 14:30"`

**Зберігання**: `%LOCALAPPDATA%\ResourceCalculator\history.json` (макс. 20 записів).

---

## 13. ComponentDisplayName

**Файл**: `ResourceCalculator.Core/Models/ComponentDisplayName.cs`
**Призначення**: Локалізація назв компонентів для UI.

**Статичні переклади** (Uk словник):
| Оригінал | Переклад |
|---|---|
| `"AS (App Server)"` | `"AS (Сервер додатків)"` |
| `"ROBOT"` | `"ROBOT (Планувальник)"` |
| `"Webrmd"` | `"Webrmd (Веб клієнт)"` |

Решта назв повертаються без зміни (канонічні технічні назви).

---

## 14. SizingMatrix

**Файл**: `ResourceCalculator.Core/Data/SizingMatrix.cs`
**Призначення**: ПОВНА матриця розмірування. Зберігається в JSON.

**SchemaVersion**: `10` (відкидає застарі матриці).

### Колекції діапазонів
| Колекція | Тип | Рядків | Опис |
|---|---|---|---|
| `MsSqlRanges` | `List<UserLoadRange>` | 12 | Діапазони для MS SQL Server |
| `AppServerRanges` | `List<UserLoadRange>` | 12 | Діапазони для App Server |
| `WebServerRanges` | `List<UserLoadRange>` | 9 | Діапазони для Web Server |
| `PostgresRanges` | `List<UserLoadRange>` | 9 | Діапазони для PostgreSQL |
| `OracleRanges` | `List<UserLoadRange>` | 9 | Діапазони для Oracle |

### Модулі документообігу
| Колекція | Тип | Опис |
|---|---|---|
| `DocumentFlowModules` | `List<ProjectModule>` | 6 модулів: App Server, ROBOT, Web, ForceBPM, LMS, HR Portal |

### Інфраструктурні вузли
| Колекція | Тип | Опис |
|---|---|---|
| `K8sNodes` | `List<InfrastructureNode>` | K8s SQL, K8s Master, K8s Worker |
| `WindowsNodes` | `List<InfrastructureNode>` | Windows SQL, Windows App, Windows Web |
| `OptionalNodes` | `List<InfrastructureNode>` | Reporting Server, HAProxy |

### Налаштування рушія
| Властивість | Тип | Опис |
|---|---|---|
| `Engine` | `EngineSettings` | Всі редаговані константи розрахунку |

**Зберігання**: `%LOCALAPPDATA%\ResourceCalculator\data\matrix.json` (атомарний запис через .tmp + .bak).

---

## 15. DocumentRequirements

**Файл**: `ResourceCalculator.Core/Data/DocumentRequirements.cs`
**Призначення**: Еталонні вимоги з документа D-AD-ADM-E для порівняння.

**Містить**:
- `SqlServer` — таблиця вимог SQL Server (12 рядків, 1-5000 користувачів): CPU, RAM, IOPS, Latency, Throughput
- `Compare(config, calculated, matrix)` — метод порівняння розрахованих ресурсів з еталоном

---

## 16. ReplicaMath

**Файл**: `ResourceCalculator.Core/Models/ProjectModule.cs` (статичний клас)
**Призначення**: Єдине джерело правди для розрахунку кількості реплік.

### Метод `Resolve(formula, fixedReplicas, userCount, auxUsers)`
Повертає кількість реплік для компонента.

**Формули**:
| Formula | Реалізація |
|---|---|
| `Fixed` | `fixedReplicas` |
| `Per25Users` | `Ceiling(userCount / 25.0)` |
| `Per50Users` | `Ceiling(userCount / 50.0)` |
| `Per100Users` | `Ceiling(userCount / 100.0)` |
| `Per1000Users` | `Ceiling(userCount / 1000.0)` |
| `OnePlusPer100` | `1 + Int(userCount / 100.0)` |
| `Per100Plus1000` | `1 + Int(userCount/100) + Int(auxUsers/1000)` |
| `Per50Plus500` | `1 + Int(userCount/50) + Int(auxUsers/500)` |

**Емпіричні формули** (load-test):
| Formula | Точки |
|---|---|
| `HrPortalGraphqlLoadTest` | 500→3, 1000→8, далі +1/125 |
| `HrPortalSmartIdLoadTest` | 500→2, 1000→14, далі +1/75 |
| `HrPortalRobotLoadTest` | ≤1000→1, далі +1/1000 |
| `LmsGraphqlLoadTest` | 50→1, 100→2, 150→3, 200→5, 250→7, далі +2/50 |

**Параметр `auxUsers`**: Допоміжна кількість користувачів для формул із перехресним зв'язком (ROBOT/WS масштабуються від HR Portal). Якщо `< 0` — використовується `userCount`.

---

## Зв'язки між моделями

```
SizingMatrix
├── List<UserLoadRange>          MsSqlRanges / AppServerRanges / WebServerRanges / Postgres / Oracle
├── List<ProjectModule>          DocumentFlowModules
│   └── List<ModuleComponent>    Components
├── List<InfrastructureNode>     K8sNodes / WindowsNodes / OptionalNodes
└── EngineSettings               Engine

ProjectConfig ──→ SizingEngine.Calculate() ──→ ResourceRequirement
                                                     ├── List<ServiceComponent>    Components
                                                     └── List<InfrastructureNode>  Infrastructure

ResourceRequirement ──→ EnvironmentReport (для кожного PROD/DEV/TEST/PreProd)
                              └── ValidationError[] (через ValidationEngine)

ConfigExportService ──→ Excel/PDF (з ResourceRequirement + EnvironmentReport[])
```
