# AGENTS.md — Пам'ять проєкту (для нових сесій)

Цей файл — швидка пам'ять. Нова сесія має почати з нього, а не перечитувати проєкт із нуля.

## Що це за проєкт

**IT-Enterprise Resource Calculator** (раніше AIResourceCalculator) — Windows WPF-застосунок (.NET 10, MVVM)
для автоматизованого розрахунку ресурсів IT-інфраструктури (CPU/RAM/диски/IOPS) за матрицею сайзингу.
Вихідні дані — документ D-AD-ADM-E та еталонний Excel-калькулятор клієнта (IT-Enterprise).

Репозиторій: `github.com/ajjs1ajjs/Calculator-servers` (гілка `master`, pуш за замовчуванням).
Робоча тека: `E:\Code\Calculator-servers`.

## Ключові факти стану (на 2026-08-26)

- **Поточна версія: 2.3.1** (Windows only; фікс CVE-2026-39959). `AppVersion` у `Directory.Build.props`.
- Останні коміти (від новіших): `dbd9cca` (main.png для обходу кешу), `74b5971` (скріншот «Параметри»), `c9f436b` (пароль при редагуванні комірки), `31acd43` (версія 2.0.2), `b1ae2c6` (вільне керування модулями), `b2ebc9e` (BOM скриптів), `d792528` (захист паролем + фікси), `026d54c` (єдиний профіль), `820a185` (ренейм + 3 вкладки + матриця).
- Тег відкату до стану до рефакторингу: `backup-before-refactor` → `git reset --hard backup-before-refactor`.
- **Тестів: 131, усі проходять** (`dotnet test ResourceCalculator.slnx -c Release`).

## Архітектура (після рефакторингу)

- **Namespace/проєкт:** `ResourceCalculator` (папки `ResourceCalculator/`, `ResourceCalculator.Tests/`, `ResourceCalculator.Installer/`).
  exe/білд: `ITE.ResourceCalculator.exe`. Раніше було `AIResourceCalculator` — повністю перейменовано, AI-згадок немає.
- **Рішення:** `ResourceCalculator.slnx` (проєкт + тести).
- **3 вкладки-кроки** (`MainWindow.xaml`): 0=Матриця, 1=Параметри розрахунку, 2=Результати.
  ⚠️ ВАЖЛИВО: після розрахунку перехід на результати — `SelectedTabIndex = 2` (не 1!). Та сама правка в `CalculatorTabControl.xaml.cs` (кнопка «Детальніше у Результати»).
- **Збірка:** `dotnet build ResourceCalculator.slnx -c Release`. Тести: `dotnet test ResourceCalculator.slnx -c Release`.
- **Запуск з коду:** `dotnet run --project ResourceCalculator/ResourceCalculator.csproj`.

## Захист матриці (фіча від користувача)

- Зміна чутливих даних матриці (Save/Recalculate/Reset) потребує пароля.
- Спроба відредагувати будь-яке значення в матриці (клік у комірку) теж потребує пароля —
  перехоплюється `Grid_BeginningEdit` у `Views/MatrixTabControl.xaml.cs` (усі DataGrid).
- `AccessService.EnsureUnlocked()` — публічний, викликається і командами, і редактором комірок.
- `ResourceCalculator/Services/AccessService.cs` — SHA-256 + сіль, файл `settings.json` у `%LOCALAPPDATA%\ResourceCalculator\data\`.
- Дефолтний пароль: `yF2jrX7inC4w`.
- Діалоги: `Views/PasswordDialog.*` (розблокування + кнопка **«Перегенерувати пароль»**) та `Views/ChangePasswordDialog.*`.
  ⚠️ Діалоги лежать у `Views/`, тож шлях до тем у їхньому XAML — `../Themes/Styles.xaml` (не `Themes/...`).
- Перегенерація: генерує новий пароль, зберігає, відкриває `mailto:` на контакти розробника:
  `yaroslav.andreichuk@gmail.com`, `andreichuk.y@it-enterprise.com`, телефон `+380979454941`.
- `AccessService` зареєстровано в DI (`App.xaml.cs`), передається в `MatrixViewModel`.

## Матриця та редагування без коду

`MatrixViewModel` / `MatrixManager` / `DataService`: все зберігається у `%LOCALAPPDATA%\ResourceCalculator\data\matrix.json`.

Редаговане через UI (без коду): діапазони MSSQL/App/Web/Postgres/Oracle (CPU/RAM/IOPS/латентність/MiB/s/InstanceCount),
компоненти-поди (назва, CPU, RAM, Perf CPU/RAM, фікс. репліки, формула-список), вузли інфраструктури
(K8s SQL/Master/Worker, Windows SQL/App/Web, Сервер звітів, HAProxy), константи рушія (`EngineSettings`:
SmartID, IOPS-профілі, ліміти SQL, pagefile-коефіцієнт, worker-вузол).

**Керування модулями/кнопками:** усі модулі (App Server / ROBOT / Web / ForceBPM / LMS / HR) та всі
опціональні вузли (Сервер звітів / SQL Secondary / HAProxy) вільно вмикаються/вимикаються користувачем
окремо — без автоматичних блокувань за типом розгортання (`IsUserToggleable` завжди `true`, HAProxy не блокується).

**Лише кодом (не матрицею):**
- Нові **типи** формул — enum `ReplicaFormula` + `ReplicaMath.Resolve` у `Models/ProjectModule.cs`.
- Еталон D-AD-ADM-E — `Data/DocumentRequirements.cs` (стовпець «За документом» у звірці).
- Сама логіка розрахунку — `Services/SizingEngine.cs`.

## Ключові рішення/обмеження

- **Єдиний профіль навантаження** (Performance). Enum `LoadProfile` має лише `Performance`.
  Basic-діапазони прибрано з матриці. Движок завжди використовує PerfCpu/PerfRamGb.
- ⚠️ **«Документообіг» (DocumentFlow) прибрано з UI-описів** (локалізація, README) — профіль один.
  Технічні назви (модулі DocumentFlowModules, product.documentflow→"Resource Calculator") лишились у коді.
- ⚠️ **Імпорт Excel прибрано** (немає кнопки; видалено ключі локалізації setup.import/status.imported/matrix.importHint).
  Експорт у Excel (.xlsx) лишився.
- `SizingMatrix.SchemaVersion = 10`. При зміні структури матриці в коді підвищувати — старі `matrix.json` відкидаються.
- SQL Server Standard ліміти: 128 ГБ RAM / 24 ядра (в `EngineSettings`, редагуються). Увага: для SQL 2025 ліміт ядер = 32 (2022 = 24) — користувач поки не просив міняти.
- Формули реплік: `ReplicaMath.Resolve` (Per25Users, Per100Users, Per50Users, Per100Plus1000, Per50Plus500, OnePlusPer100, Per1000Users, LmsGraphqlLoadTest, Fixed).
- Worker-вузли K8s: `Max(1, ceil(max(PodCpu/workerCpu, PodRam/workerRam)))`. Master — 2 CPU/4 ГБ.
- Pagefile app/web = CEILING(RAM × 4, 10) (коефіцієнт/округлення редагуються в EngineSettings).
- Диски БД: OS/Logs+TempDB/MainData/Content; DB size → MainData=точно, Logs=25%.
- Windows = чисті VM без подів (PodCpu=0). Hybrid = K8s (без app/web/БД) + Windows (app/web+БД). Один master.
- SmartID — один центральний под: `ceil(users/25)` × CPU 0.2 / RAM 0.5 (з EngineSettings).

## Скрипти та реліз

- `release.ps1` — повний реліз (Windows-only): перевірка версії/тегів → build → test → publish WPF exe + Avalonia win-x64 zip + MSI → push+tag → GitHub Release (exe+MSI+avalonia-win.zip). **Версію бампати в `Directory.Build.props` ПЕРЕД релізом.**
- ⚠️ Нотатки GitHub-релізу (`-ReleaseNotes`) — **тільки українською** (README/CHANGELOG/UI українські). Уникати російських формулювань (Версия, переимен, инсталятор, расчёт, Документооборот тощо).
- `sign.ps1` — підпис exe. Самопідписаний сертифікат `CN=IT-Enterprise ResourceCalculator` (25 років), `.cer` у корені.
- **⚠️ Кодування `.ps1`**: файли мають бути **UTF-8 з BOM** (PowerShell 5.1 інакше ламає кирилицю). Не перезаписувати через Set-Content без BOM.
- `.github/workflows/ci.yml` — CI: build + test + coverage (ReportGenerator) + publish (Windows-only). Шляхи: `ResourceCalculator/...`.
- **⚠️ Кирилиця в коді**: файли `.cs/.xaml/.csproj` мають бути UTF-8 (без BOM ок). Не використовувати PowerShell `Set-Content` для перезапису .cs/.xaml — псує кодування; використовувати edit-інструменти або `[System.IO.File]::WriteAllText(..., UTF8)`.

## Контакти та поточні домовленості

- Розробник: пошти `yaroslav.andreichuk@gmail.com`, `andreichuk.y@it-enterprise.com`, тел. `+380979454941`.
- Користувач тестує v2.0.2. Наступні зміни/релізи — за його відгуком після тестів.
- Пароль матриці можна змінювати через UI (кнопка «Змінити пароль»), дефолт див. вище.
- README (`README.md`) містить бейджі (Release/Downloads/CI/Tests 135/License), скріншот `docs/screenshots/main.png`, банер `docs/banner.svg`.
- ⚠️ GitHub кешує зображення через camo. Якщо прев'ю/фото на сторінці «не те»: додавати кеш-бастер `?v=N` до URL у README, а найнадійніше — **перейменувати файл** (новий шлях = новий URL без кешу). Останній скріншот — `main.png` (вкладка «Параметри розрахунку»).
- ⚠️ Оновлення `AGENTS.md`: після кожної значущої зміни оновлювати цей файл (версія, коміти, рішення). Нова сесія має спершу прочитати його.
