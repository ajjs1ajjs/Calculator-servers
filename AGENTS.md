# AGENTS.md — Пам'ять проєкту (для нових сесій)

Цей файл — швидка пам'ять. Нова сесія має почати з нього, а не перечитувати проєкт із нуля.

## Документація (читати замість коду)

> **Правило**: Перед тим як читати вихідний код, звернись до цих MD-файлів. Вони містять всю потрібну інформацію без витрат токенів.

| Документ | Посилання | Що містить |
|---|---|---|
| **ARCHITECTURE.md** | [→](ARCHITECTURE.md) | Архітектура, структура рішення, DI-контейнер, CI/CD, збереження даних |
| **DATA-MODELS.md** | [→](DATA-MODELS.md) | Детальний опис кожної моделі даних, властивості, зв'язки, формули |
| **IMPLEMENTATION.md** | [→](IMPLEMENTATION.md) | Як працюють сервіси: SizingEngine, DataService, ConfigExport, Validation тощо |
| **FUNCTIONS.md** | [→](FUNCTIONS.md) | Швидкий довідник по всіх публічних методах кожного класу |
| **TESTS.md** | [→](TESTS.md) | Структура тестів, що перевіряє кожен тестовий файл |

### Як користуватися
1. Нова сесія → прочитай `AGENTS.md` (цей файл)
2. Потрібна архітектура → читай `ARCHITECTURE.md`
3. Потрібна модель даних → читай `DATA-MODELS.md`
4. Потрібна логіка сервісу → читай `IMPLEMENTATION.md`
5. Потрібен метод → читай `FUNCTIONS.md`
6. Потрібні тести → читай `TESTS.md`
7. Тільки якщо цих файлів недостатньо → читай вихідний код

## Що це за проєкт

**IT-Enterprise Resource Calculator** (раніше AIResourceCalculator) — десктоп-застосунок (.NET 10, Avalonia, MVVM)
для автоматизованого розрахунку ресурсів IT-інфраструктури (CPU/RAM/диски/IOPS) за матрицею сайзингу.
Вихідні дані — документ D-AD-ADM-E та еталонний Excel-калькулятор клієнта (IT-Enterprise).

Репозиторій: `github.com/ajjs1ajjs/Calculator-servers` (гілка `master`, pуш за замовчуванням).
Робоча тека: `E:\Code\Calculator-servers`.

## Виправлення міграції на Avalonia (2026-09-14, ще не закомічено)

- `Themes/AppStyles.xaml` — корінь `<Styles>`; `x:Key` у `Style` заборонено (класи `card/cardheader/iconbadge/iconglyph/chip/pillgroup/kpicard/envtoggle`). `ThemeService` вантажить його як `Styles` у `app.Styles`. Глобальний `Border` без класу заборонено — ламає шаблони (DataGrid).
- Таблицям матриці явно задано `HeadersVisibility="Column"` (дефолт Avalonia — None); автоколонки увімкнено як у WPF (широкі таблиці з дублями). `FluentTheme` + тема DataGrid підключені в `App.xaml`; `RequestedThemeVariant="Light"`.
- Глобальний `Style Selector="Border"` ламає лейаут усередині шаблонів (DataGrid малював текст нульовою шириною) → замінено на `Border.card`, клас `card` додано всім карткам у XAML.
- `Expander` за замовчуванням `Left` → у стилі `Expander` додано `Stretch` (+ `HorizontalContentAlignment`).
- Кастомний шаблон `Button` — у `ContentPresenter` потрібен `Content="{TemplateBinding Content}"`, інакше кнопки порожні.
- Побите кодування кирилиці (UTF-8 прочитане як CP1251) у `CalculatorTabControl.xaml`, `ResultsTabControl.xaml`, `UpdateProgressDialog.xaml` — відновлено. Не писати .xaml/.cs через PowerShell `Set-Content`.
- Таблицям матриці явно задано `AutoGenerateColumns="False" HeadersVisibility="Column".

## Оновлення всередині програми (2026-09-14, ще не закомічено)

- Жодних переходів у браузер/GitHub: `UpdateCheckService` резолвить прямий `browser_download_url` exe-ассета + парсить `body` (нотатки) і `size`; `SelfUpdateService` качає, перевіряє SHA256 і підміняє exe bat-скриптом з авторестартом.
- `Views/UpdateAvailableDialog.*` — вікно «Доступне оновлення»: чипи поточна→нова версія, розмір файлу, «Що нового» (scroll), кнопки «Пізніше» / «Оновити зараз». `App.CheckForUpdatesAsync` показує його замість `MessageBox YesNo`.
- `Views/UpdateProgressDialog.*` — редизайн: етап (завантаження/встановлення), прогрес-бар + `%` + `завантажено/всього`, «Скасувати»; після помилки — текст помилки + «Закрити» / «Спробувати ще» (`RetryRequested`, цикл у `App.StartUpdateAsync`). Після успіху — `SetCompleted()` + пауза 1.5с, щоб було видно, потім `Environment.Exit(0)`.
- Локалізація: `update.message` переформульовано на автозавантаження (без «відкрити сторінку»); нові ключі `update.updateNow/later/whatsNew/currentVersion/newVersion/downloading/installing/completed/cancel/close/retry/downloadedOf` (uk+en).
- `UpdateInfo` розширено: `(Version, DownloadUrl, ReleaseNotes?, SizeBytes)`.

## Відкладений реліз 1 жовтня 2026 (ліміти Actions)

- Код чекає в гілці `release/oct-1`; у `main` лише guard `[skip actions]` у `ci.yml` + `.github/workflows/deferred-release.yml`.
- 2026-10-01 03:00 UTC scheduler хмарно: мердж `release/oct-1` (`-X theirs` + повернення guard), бамп `AppVersion → 2.4.13`, тег `v2.4.13` → штатний `release.yml` публікує реліз. Ручний запуск — кнопка Run workflow.
- ⚠️ До 1 жовтня не пушити в `main` (щоб не з'їсти ліміти і не розійтись зі staging).

## Ключові факти стану (на 2026-09-14)

- **Поточна версія: 2.4.12** (Windows portable, тільки світла тема; міграція на Avalonia — код у робочій теці, ще не закомічено). `AppVersion` у `Directory.Build.props`.
- Останні коміти (від новіших): `bee9e9e` (документація ARCHITECTURE/DATA-MODELS/IMPLEMENTATION/FUNCTIONS/TESTS), `ce94295` (один шлях релізу), `02f99f1` (вбудоване оновлення, чистка репозиторію, 2.4.12), `ffd4c9d` (Reset без пароля, прибрано ChangePassword).
- Тег відкату до стану до рефакторингу: `backup-before-refactor` → `git reset --hard backup-before-refactor`.
- **Тестів: 130, усі проходять** (`dotnet test ResourceCalculator.slnx -c Release`).

## Архітектура (після рефакторингу)

- **Namespace/проєкт:** `ResourceCalculator` (папки `ResourceCalculator/` — Avalonia UI, `ResourceCalculator.Core/` — уся логіка, `ResourceCalculator.Tests/`).
  exe/білд: `ITE.ResourceCalculator.exe`. Раніше було `AIResourceCalculator` — повністю перейменовано, AI-згадок немає.
- **Рішення:** `ResourceCalculator.slnx` (проєкт + тести).
- ⚠️ Lifetime — тільки `IClassicDesktopStyleApplicationLifetime` (`Program` стартує `StartWithClassicDesktopLifetime`); `ISingleView` не використовувати — з ним вікно не створюється.
- Іконки `Segoe MDL2 Assets` заборонені (Windows-only): тик — `✓`, логотип — `◈`, решта декоративних прибрана. Емодзі в `MessageBox` — ок.
- `Themes/ThemeService` — тільки світла тема (Latte), `DarkTheme.xaml` видалено, кнопки-перемикача теми немає. `Themes/Styles.xaml` видалено, є `Themes/AppStyles.xaml`.
- **3 вкладки-кроки** (`MainWindow.xaml`): 0=Матриця, 1=Параметри розрахунку, 2=Результати.
  ⚠️ ВАЖЛИВО: після розрахунку перехід на результати — `SelectedTabIndex = 2` (не 1!). Та сама правка в `CalculatorTabControl.xaml.cs` (кнопка «Детальніше у Результати»).
- **Збірка:** `dotnet build ResourceCalculator.slnx -c Release`. Тести: `dotnet test ResourceCalculator.slnx -c Release`.
- **Запуск з коду:** `dotnet run --project ResourceCalculator/ResourceCalculator.csproj`.
- **Публікація (Windows):** `dotnet publish ResourceCalculator/ResourceCalculator.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/win`

## Захист матриці (фіча від користувача)

- Зміна чутливих даних матриці (Save/Recalculate/редагування клітинки) потребує пароля; Reset — без пароля. Редагування клітинки — через `BeginningEdit` (`MatrixTabControl.xaml.cs`): скасування + асинхронний пароль через `Dispatcher.UIThread.Post` (діалог всередині події DataGrid зависає) + повторний `BeginEdit` після розблокування.
- Усі таблиці матриці заблоковані (`IsReadOnly={Binding MatrixVM.IsUnlocked, Converter=BoolInverse}`) — розблоковуються на сесію після `EnsureUnlockedAsync()` (парольний діалог).
- `AccessService.EnsureUnlockedAsync()` викликається командами Save/Recalculate; синхронний `EnsureUnlocked()` лишено для сумісності.
- `ResourceCalculator.Core/Services/AccessService.cs` — SHA-256 + сіль, файл `settings.json` у `%LOCALAPPDATA%\ResourceCalculator\data\`.
- Дефолтний пароль: `yF2jrX7inC4w`.
- Діалог: `Views/PasswordDialog.*` (розблокування + кнопка **«Перегенерувати пароль»**). `ChangePasswordDialog` і кнопка «Змінити пароль» прибрані — лишилися тільки мертві рядки локалізації `access.change*`.
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

- **Єдиний шлях релізу — push тега `vX.Y.Z`.** Локальних реліз-скриптів немає навмисно: `release.ps1` прибрано, бо він конфліктував із workflow (обидва робили `gh release create`).
  Порядок: бампнути `AppVersion` у `Directory.Build.props` → коміт → `git push origin main` → `git tag vX.Y.Z && git push origin vX.Y.Z`.
- `.github/workflows/release.yml` — реліз на push тега `v*`: build → test → publish self-contained exe (тільки win-x64) → GitHub Release з одним артефактом.
  Нотатки генеруються автоматично (`--generate-notes`).
- `.github/workflows/ci.yml` — CI на push у `main`/PR: build + test + coverage (ReportGenerator) + перевірка вразливих пакетів + publish exe як артефакт.
- ⚠️ Тексти релізів/CHANGELOG — **тільки українською**. Уникати російських формулювань (Версия, переимен, инсталятор, расчёт, Документооборот тощо).
- Реліз без підпису: MSI-інсталятор, `sign.ps1` і самопідписаний сертифікат прибрано разом із `release.ps1` — SmartScreen попереджатиме, доки не буде сертифіката від CA.
- **⚠️ Кирилиця в коді**: файли `.cs/.xaml/.csproj` мають бути UTF-8 (без BOM ок). Не використовувати PowerShell `Set-Content` для перезапису .cs/.xaml — псує кодування; використовувати edit-інструменти або `[System.IO.File]::WriteAllText(..., UTF8)`.

## Контакти та поточні домовленості

- Розробник: пошти `yaroslav.andreichuk@gmail.com`, `andreichuk.y@it-enterprise.com`, тел. `+380979454941`.
- Користувач тестує v2.0.2. Наступні зміни/релізи — за його відгуком після тестів.
- Пароль матриці змінюється тільки перегенерацією через діалог розблокування (кнопки «Змінити пароль» немає), дефолт див. вище.
- README (`README.md`) містить бейджі (Release/Downloads/CI/Tests 130/License/Platform), скріншот `docs/screenshots/main.png`, банер `docs/banner.svg`.
- ⚠️ GitHub кешує зображення через camo. Якщо прев'ю/фото на сторінці «не те»: додавати кеш-бастер `?v=N` до URL у README, а найнадійніше — **перейменувати файл** (новий шлях = новий URL без кешу). Останній скріншот — `main.png` (вкладка «Параметри розрахунку»).
- ⚠️ Оновлення `AGENTS.md`: після кожної значущої зміни оновлювати цей файл (версія, коміти, рішення). Нова сесія має спершу прочитати його.
