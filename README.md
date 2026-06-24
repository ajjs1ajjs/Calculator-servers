# Калькулятор ресурсів інфраструктури

**Репозиторій:** [github.com/ajjs1ajjs/Calculator-servers](https://github.com/ajjs1ajjs/Calculator-servers)

Десктоп-застосунок (Windows, WPF) для автоматизованого розрахунку ресурсів IT-інфраструктури
на основі матриці сайзингу. Підтримує розгортання в Kubernetes, Windows та гібридному режимі,
бере вихідні дані з Excel-калькулятора й формує звіт із вимогами до CPU, RAM, дисків та IOPS.

## Можливості

- **Розрахунок ресурсів** — CPU, RAM, дискова підсистема та IOPS для K8s / Windows / Hybrid.
  У гібриді сервери додатків і веб (IIS) — на Windows-VM, ForceBPM та інші сервіси — на K8s, БД — на Windows.
- **Середовища** — PROD завжди; за вибором DEV (окрема к-сть ліцензій), TEST (зменшений PROD, диск ≥ PROD + бекап-резерв)
  та PreProd (на 20% потужніший за TEST), з порівняльною таблицею.
- **Типи СУБД** — MS SQL Server, PostgreSQL, Oracle 19c.
- **Профілі навантаження** — Стандарт (Basic) та Документообіг (Performance), з порівнянням.
- **Вимоги до дисків** — окрема розкладка для БД (OS / Logs+TempDB / Data / Content) та файл підкачки для app/web-вузлів.
- **Імпорт Excel** — завантаження та редагування матриці сайзингу.
- **Експорт звіту** — Excel (.xlsx, для тендерних документів), XML (для імпорту) та HTML.
- **Історія розрахунків** — останні 20 розрахунків зберігаються локально.
- **Локалізація** — українська та англійська мови.

## Технології

- C# / WPF, .NET 10, патерн MVVM
- Microsoft.Extensions.DependencyInjection (DI-контейнер)
- EPPlus 7.6 (читання Excel)

## Збірка та запуск

```bash
# Запуск із вихідного коду
dotnet run --project AIResourceCalculator/AIResourceCalculator.csproj

# Збірка та тести
dotnet build AIResourceCalculator.slnx -c Release
dotnet test  AIResourceCalculator.slnx -c Release

# Публікація self-contained exe (артефакт у git не зберігається)
dotnet publish AIResourceCalculator/AIResourceCalculator.csproj -c Release --output publish
```

Потрібен .NET SDK 10 (версія зафіксована у [`global.json`](global.json)).

### Цифровий підпис exe

Скрипт [`sign.ps1`](sign.ps1) підписує опублікований `publish\AIResourceCalculator.exe`:

```powershell
# Самопідписаний сертифікат (для внутрішнього використання)
./sign.ps1

# Корпоративний / придбаний сертифікат — підпис, якому довірятимуть інші ПК
./sign.ps1 -PfxPath C:\certs\company.pfx -PfxPassword (Read-Host -AsSecureString)
```

- **Самопідписаний** підпис вбудовується у файл, але на чужих ПК SmartScreen усе одно
  попереджатиме, доки сертифікат не додано в їхній Trusted Root (прапорець `-TrustLocally`
  додає його лише на поточну машину).
- Щоб попередження зникали на будь-якому ПК, потрібен сертифікат від довіреного центру (CA) —
  передайте його через `-PfxPath`.

## Структура проекту

| Каталог | Призначення |
|---------|-------------|
| `AIResourceCalculator/Models` | Моделі даних (вузли, діапазони навантаження, модулі). |
| `AIResourceCalculator/Services` | Бізнес-логіка: рушій сайзингу, масштабування середовищ, експорт Excel/XML/HTML, валідація, вимоги до дисків. |
| `AIResourceCalculator/ViewModels` | ViewModel'и MVVM. |
| `AIResourceCalculator/Views` | XAML-вкладки інтерфейсу. |
| `AIResourceCalculator/Data` | Матриця сайзингу за замовчуванням та імпорт Excel. |
| `AIResourceCalculator/Localization` | Рядки інтерфейсу (uk/en). |
| `AIResourceCalculator.Tests` | Модульні тести (xUnit). |

Історію змін див. у [CHANGELOG.md](CHANGELOG.md).
