# AI Resource Calculator

**Репозиторій:** [github.com/ajjs1ajjs/Calculator-servers](https://github.com/ajjs1ajjs/Calculator-servers)

AI Resource Calculator — програма для автоматизованого розрахунку ресурсів IT-інфраструктури на основі Excel-калькулятора. Підтримує K8s, Windows та гібридні розгортання з AI-рекомендаціями та генерацією IaC.

## Функціонал

- **Калькулятор ресурсів** — розрахунок CPU, RAM, Storage, IOPS для K8s/Windows/Hybrid
- **AI Advisor** — рекомендації (rule-based + OpenAI / Claude / Ollama)
- **Порівняння профілів** — Basic vs Performance
- **Візуальна схема мережі** — кольорова діаграма інфраструктури
- **Валідація (Resource Guard)** — перевірка відповідності ресурсів
- **Експорт** — TXT, HTML (print to PDF), SVG, Mermaid
- **Excel імпорт** — імпорт та редагування матриці даних
- **AI Помічник** — текстовий опис інфраструктури

## Технології

- C# WPF (.NET 10), MVVM
- EPPlus 7.6.0 (Excel)
- OpenAI / Claude / Ollama API

## Запуск

```bash
dotnet run --project "AIResourceCalculator\AIResourceCalculator.csproj"
```
