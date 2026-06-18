# Калькулятор ресурсів інфраструктури

**Репозиторій:** [github.com/ajjs1ajjs/Calculator-servers](https://github.com/ajjs1ajjs/Calculator-servers)

Десктоп-застосунок (Windows, WPF) для автоматизованого розрахунку ресурсів IT-інфраструктури
на основі матриці сайзингу. Підтримує розгортання в Kubernetes, Windows та гібридному режимі,
бере вихідні дані з Excel-калькулятора й формує звіт із вимогами до CPU, RAM, дисків та IOPS.

## Можливості

- **Розрахунок ресурсів** — CPU, RAM, дискова підсистема та IOPS для K8s / Windows / Hybrid.
- **Типи СУБД** — MS SQL Server, PostgreSQL, Oracle 19c.
- **Профілі навантаження** — Стандарт (Basic) та Документообіг (Performance), з порівнянням.
- **Вимоги до дисків** — окрема розкладка для БД (OS / Logs+TempDB / Data / Content) та файл підкачки для app/web-вузлів.
- **Імпорт Excel** — завантаження та редагування матриці сайзингу.
- **Експорт звіту** — TXT та HTML.
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
dotnet publish AIResourceCalculator/AIResourceCalculator.csproj -c Release
```

Потрібен .NET SDK 10 (версія зафіксована у [`global.json`](global.json)).

## Структура проекту

| Каталог | Призначення |
|---------|-------------|
| `AIResourceCalculator/Models` | Моделі даних (вузли, діапазони навантаження, модулі). |
| `AIResourceCalculator/Services` | Бізнес-логіка: рушій сайзингу, експорт TXT/HTML, валідація, вимоги до дисків. |
| `AIResourceCalculator/ViewModels` | ViewModel'и MVVM. |
| `AIResourceCalculator/Views` | XAML-вкладки інтерфейсу. |
| `AIResourceCalculator/Data` | Матриця сайзингу за замовчуванням та імпорт Excel. |
| `AIResourceCalculator/Localization` | Рядки інтерфейсу (uk/en). |
| `AIResourceCalculator.Tests` | Модульні тести (xUnit). |

Історію змін див. у [CHANGELOG.md](CHANGELOG.md).
