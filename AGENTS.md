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

## Що зроблено в `release/oct-1` (закомічено `398ecf2`, чекає мерджу 1 жовтня)

- **Міграція на Avalonia 12.1.2** (`c98f90b`): `Program.cs` (`StartWithClassicDesktopLifetime`), `App.xaml` (`FluentTheme` + DataGrid-тема, `RequestedThemeVariant="Light"`), `Dialogs/DialogService.cs` + `Dialogs/ThemeService.cs` (замість `Wpf*`), `Themes/AppStyles.xaml` (корінь `<Styles>`, класи `card/chip/pillgroup/...`), `DarkTheme.xaml`/`Styles.xaml` видалено.
- Нюанси Avalonia: `x:Key` у `Style` заборонено; глобальний `Border` без класу ламає шаблони (DataGrid малював текст нульовою шириною) → `Border.card`; таблицям матриці явно `HeadersVisibility="Column"`; `Expander` → `Stretch`; кастомний шаблон `Button` потребує `Content="{TemplateBinding Content}"`.
- Не писати .xaml/.cs через PowerShell `Set-Content` — псує кирилицю (було відновлення UTF-8/CP1251 в трьох файлах); тільки edit-інструменти або `[System.IO.File]::WriteAllText(..., UTF8)`.
- **Оновлення всередині програми** (`c98f90b`): `UpdateCheckService` резолвить прямий `browser_download_url` exe-ассета + `body`/`size`; `UpdateInfo(Version, DownloadUrl, ReleaseNotes?, SizeBytes)`; `Views/UpdateAvailableDialog` (чипи версій, розмір, «Що нового», Пізніше/Оновити зараз); `Views/UpdateProgressDialog` (етап, `%`, завантажено/всього, Скасувати; після помилки — Закрити/Спробувати ще, retry-цикл в `App.StartUpdateAsync`; після успіху пауза 1.5с → `Environment.Exit(0)`); нові ключі локалізації `update.*` + `dialog.yes/no` (uk+en).
- **Іконка exe** (`80d14d9`): потрібні ОДНОЧАСНО `<ApplicationIcon>icon.ico</ApplicationIcon>` і `TargetFramework=net10.0-windows` — з чистим `net10.0` MSBuild мовчки ігнорує ApplicationIcon.

## Відкладений реліз 1 жовтня 2026 (ліміти Actions)

- Код чекає в гілці `release/oct-1` (поточна гілка, в синхроні з origin); у `main` лише guard `[skip actions]` у `ci.yml` + `.github/workflows/deferred-release.yml`. У staging-гілці цих файлів свідомо немає (guard лише в main, scheduler живе тільки в main) — не копіювати їх у `release/oct-1`.
- 2026-10-01 03:00 UTC scheduler хмарно: мердж `release/oct-1` (`-X theirs` + повернення guard), бамп `AppVersion → 2.4.13`, тег `v2.4.13` → штатний `release.yml` публікує реліз. Ручний запуск — кнопка Run workflow. ⚠️ Після мерджу staging-версія файлів перемагає — доки комітити тільки в `release/oct-1`, не в `main`.
- ⚠️ До 1 жовтня не пушити в `main` (щоб не з'їсти ліміти і не розійтись зі staging). Пуші в `release/*` CI не тригерять (CI слухає лише main/master) — вони безкоштовні.

## Ключові факти стану (на 2026-09-15, гілка `release/oct-1`)

- **Поточна версія: <!-- AUTO:app-version -->2.4.12<!-- /AUTO -->** (`AppVersion` у `Directory.Build.props`; 2.4.13 буде виставлено автоматично 1 жовтня). UI — Avalonia 12.1.2, тільки світла тема, Windows portable.
- Останні коміти (від новіших): `398ecf2` (нотатка про іконку), `80d14d9` (іконка ApplicationIcon + net10.0-windows), `c98f90b` (Avalonia-міграція + вікна оновлення — стейджинг для 1 жовтня), `bee9e9e` (документація ARCHITECTURE/DATA-MODELS/IMPLEMENTATION/FUNCTIONS/TESTS), далі `ce94295`/`02f99f1`/`ffd4c9d` (див. `git log`).
- Тег відкату до стану до рефакторингу: `backup-before-refactor` → `git reset --hard backup-before-refactor`.
- **Тестів: <!-- AUTO:tests-total -->157<!-- /AUTO -->, усі проходять** (`dotnet test ResourceCalculator.slnx -c Release`).

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

- Зміна чутливих даних матриці (Save/Recalculate/Reset/додавання рядків/редагування клітинки) потребує пароля. Редагування клітинки — через `BeginningEdit` (`MatrixTabControl.xaml.cs`): скасування + асинхронний пароль через `Dispatcher.UIThread.Post` (діалог всередині події DataGrid зависає) + повторний `BeginEdit` після розблокування.
- Усі таблиці матриці заблоковані (`IsReadOnly={Binding MatrixVM.IsUnlocked, Converter=BoolInverse}` — конвертер зареєстровано в `App.xaml`), панель Engine — `IsEnabled` від `IsUnlocked`; розблоковуються на сесію після `EnsureUnlockedAsync()` (парольний діалог). Синхронний шим `EnsureUnlocked()` прибрано (дедлок на UI-потоці).
- `AccessService` — PBKDF2-SHA256 (210k ітерацій) + сіль 16Б + поле `Iterations`, файл `settings.json` у `%LOCALAPPDATA%\ResourceCalculator\data\` з ACL тільки поточному користувачеві. Легасі SHA-256 приймається лише для міграції (одразу перехешовується).
- Вбудованого дефолтного пароля НЕМАЄ (fail closed): за відсутності `settings.json` перший запуск показує режим СТВОРЕННЯ пароля (мінімум 12 символів). Невдалі спроби — прогресивна затримка 1с→30с, після 10 — блок на 5 хв.
- Діалог: `Views/PasswordDialog.*` (розблокування / створення; TextBox+PasswordChar — штатного PasswordBox в Avalonia 12 немає).
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

## Протокол Docs-sync (щоб доки не протухали)

1. **Старт сесії** → прочитай цей файл + таблицю свіжості нижче. Якщо штамп доку старіший за `git log` по коду — доку не довіряй, читай код.
2. **Після кожної зміни коду/функціоналу** → запусти `powershell -File scripts/Update-Docs.ps1` (перераховує к-сть тестів, версію, штампи, ловить розсинхрон API). Вручну допиши тільки змістовні зміни (нові методи/флоу), цифри скрипт підставить сам.
3. **Коміт** → доки комітяться разом з кодом одним комітом. Локальний pre-commit хук (`.githooks/`, установка: `git config core.hooksPath .githooks`) сам дооновлює маркери і додає їх у коміт.
4. **CI** → `docs-sync.yml` на PR перевіряє `Update-Docs.ps1 -Check` (секунди на ubuntu, без збірки, ліміти не їсть).

### Таблиця свіжості (оновлюється скриптом)

| Документ | Звірено з комітом | Дата |
|---|---|---|
| ARCHITECTURE.md | <!-- AUTO:arch-commit -->`10355a9`<!-- /AUTO --> | <!-- AUTO:arch-date -->2026-09-15<!-- /AUTO --> |
| DATA-MODELS.md | <!-- AUTO:data-commit -->`10355a9`<!-- /AUTO --> | <!-- AUTO:data-date -->2026-09-15<!-- /AUTO --> |
| IMPLEMENTATION.md | <!-- AUTO:impl-commit -->`10355a9`<!-- /AUTO --> | <!-- AUTO:impl-date -->2026-09-15<!-- /AUTO --> |
| FUNCTIONS.md | <!-- AUTO:func-commit -->`10355a9`<!-- /AUTO --> | <!-- AUTO:func-date -->2026-09-15<!-- /AUTO --> |
| TESTS.md | <!-- AUTO:tests-commit -->`10355a9`<!-- /AUTO --> | <!-- AUTO:tests-date -->2026-09-15<!-- /AUTO --> |
