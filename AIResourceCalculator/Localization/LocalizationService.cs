using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AIResourceCalculator.Localization;

public class LocalizationService : INotifyPropertyChanged
{
    private static readonly Dictionary<string, string> StringsUk = new()
    {
        ["app.title"] = "AI Resource Calculator",
        ["app.subtitle"] = "Розрахунок · AI · Рекомендації",
        ["tab.matrixTitle"] = "1. База даних",
        ["tab.setupTitle"] = "2. Калькулятор",
        ["tab.resultsTitle"] = "3. Результати",
        ["tab.aiQueryTitle"] = "4. AI Помічник",
        ["setup.title"] = "Параметри розрахунку",
        ["setup.users"] = "Кількість користувачів:",
        ["setup.deployment"] = "Тип розгортання:",
        ["setup.ha"] = "Висока доступність:",
        ["setup.haHint"] = "Увімкнути HA (3+ вузли)",
        ["setup.overprov"] = "Коефіцієнт резервування:",
        ["setup.calculate"] = "Розрахувати ресурси",
        ["deploy.k8s"] = "Kubernetes",
        ["deploy.windows"] = "Windows",
        ["deploy.hybrid"] = "Гібрид (K8s + Windows)",
        ["profile.basic"] = "Базовий",
        ["profile.performance"] = "Продуктивний",
        ["results.title"] = "Результати розрахунку",
        ["results.totalVcpu"] = "Всього vCPU",
        ["results.totalRam"] = "Всього RAM",
        ["results.totalStorage"] = "Всього диски",
        ["results.totalIops"] = "Всього IOPS",
        ["results.infra"] = "Інфраструктура",
        ["results.compare"] = "Порівняти профілі",
        ["results.compareTitle"] = "Порівняння профілів",
        ["results.recommended"] = "Рекомендація",
        ["results.aiTitle"] = "AI Advisor — Рекомендації",
        ["results.aiAnalyzing"] = "AI аналізує вашу інфраструктуру...",
        ["col.name"] = "Назва",
        ["col.cpu"] = "vCPU",
        ["col.ram"] = "RAM",
        ["col.count"] = "К-сть",
        ["col.storage"] = "Диски (ГБ)",
        ["col.nodeType"] = "Тип вузла",
        ["col.nodes"] = "Вузли",
        ["col.minUsers"] = "Від",
        ["col.maxUsers"] = "До",
        ["col.ramMin"] = "RAM Min",
        ["col.ramRec"] = "RAM Rec",
        ["col.iops"] = "IOPS",
        ["col.latency"] = "Затримка",
        ["col.category"] = "Категорія",
        ["col.replicas"] = "Репліки",
        ["setup.import"] = "Імпорт Excel",
        ["status.ready"] = "Готово",
        ["status.calculated"] = "Розраховано: {0} користувачів, {1} vCPU, {2} ГБ RAM",
        ["status.imported"] = "Імпортовано з Excel",
        ["status.copied"] = "Скопійовано в буфер",
        ["status.saved"] = "Збережено в {0}",
        ["status.aiEnabled"] = "AI увімкнено: {0}",
        ["status.aiDisabled"] = "AI на правилах",
        ["tooltip.users"] = "Кількість активних користувачів (1-5000)",
        ["tooltip.deployment"] = "Kubernetes: контейнери. Windows: VM. Гібрид: обидва",
        ["tooltip.ha"] = "Вмикає 3+ вузли для відмовостійкості",
        ["tooltip.overprov"] = "Додатковий запас ресурсів",
        ["tooltip.calculate"] = "Запустити розрахунок ресурсів",
        ["tooltip.langSwitch"] = "Змінити мову",
        ["tooltip.aiSettings"] = "Налаштувати AI провайдера",
        ["matrix.info"] = "Редагуйте дані матриці розрахунків. Імпортуйте з Excel або вводьте вручну.",
        ["matrix.msSql"] = "MSSQL — діапазони ресурсів",
        ["matrix.performance"] = "MSSQL — продуктивний профіль",
        ["matrix.k8sComponents"] = "K8s компоненти",
        ["matrix.infra"] = "Інфраструктура",
        ["matrix.save"] = "Зберегти матрицю",
        ["matrix.reset"] = "Скинути",
        ["modules.title"] = "Модулі",
        ["ai.badgeEnabled"] = "AI Online",
        ["ai.badgeDisabled"] = "Правила (offline)",
        ["ai.settingsBtn"] = "AI Settings",
        ["ai.noData"] = "Запустіть розрахунок для отримання рекомендацій",
        ["aiQuery.title"] = "AI Помічник",
        ["aiQuery.desc"] = "Опишіть вашу інфраструктуру природною мовою. AI проаналізує та заповнить параметри.",
        ["aiQuery.analyze"] = "Проаналізувати",
        ["aiQuery.result"] = "Результат аналізу",
        ["aiQuery.apply"] = "Застосувати до калькулятора",
        ["aiQuery.templates"] = "Швидкі шаблони (без AI):",
        ["aiQuery.tpl1"] = "Система на 200 користувачів з LMS та HR",
        ["aiQuery.tpl2"] = "Високонавантажена система на 1000 користувачів",
        ["aiQuery.tpl3"] = "Мінімальна система на 25 користувачів",
        ["aiQuery.offline"] = "Увімкніть Real AI в налаштуваннях для аналізу тексту, або використайте шаблони.",
        ["diagram.title"] = "Діаграма інфраструктури",
        ["diagram.show"] = "Схема мережі",
        ["diagram.copyMermaid"] = "Копіювати Mermaid",
        ["dialog.matrixSaved"] = "Матрицю збережено",
        ["settings.ai"] = "Налаштування AI",
    };

    private static readonly Dictionary<string, string> StringsEn = new()
    {
        ["app.title"] = "AI Resource Calculator",
        ["app.subtitle"] = "Sizing · AI · Recommendations",
        ["tab.matrixTitle"] = "1. Data Matrix",
        ["tab.setupTitle"] = "2. Calculator",
        ["tab.resultsTitle"] = "3. Results",
        ["tab.aiQueryTitle"] = "4. AI Assistant",
        ["setup.title"] = "Calculation Parameters",
        ["setup.users"] = "Number of Users:",
        ["setup.deployment"] = "Deployment Type:",
        ["setup.ha"] = "High Availability:",
        ["setup.haHint"] = "Enable HA (3+ nodes)",
        ["setup.overprov"] = "Overprovisioning Factor:",
        ["setup.calculate"] = "Calculate Resources",
        ["deploy.k8s"] = "Kubernetes",
        ["deploy.windows"] = "Windows",
        ["deploy.hybrid"] = "Hybrid (K8s + Windows)",
        ["profile.basic"] = "Basic",
        ["profile.performance"] = "Performance",
        ["results.title"] = "Resource Requirements",
        ["results.totalVcpu"] = "Total vCPU",
        ["results.totalRam"] = "Total RAM",
        ["results.totalStorage"] = "Total Storage",
        ["results.totalIops"] = "Total IOPS",
        ["results.infra"] = "Infrastructure",
        ["results.compare"] = "Compare Profiles",
        ["results.compareTitle"] = "Profile Comparison",
        ["results.recommended"] = "Recommended",
        ["results.aiTitle"] = "AI Advisor — Recommendations",
        ["results.aiAnalyzing"] = "AI is analyzing your infrastructure...",
        ["col.name"] = "Name",
        ["col.cpu"] = "vCPU",
        ["col.ram"] = "RAM",
        ["col.count"] = "Count",
        ["col.storage"] = "Storage (GB)",
        ["col.nodeType"] = "Node Type",
        ["col.nodes"] = "Nodes",
        ["col.minUsers"] = "From",
        ["col.maxUsers"] = "To",
        ["col.ramMin"] = "RAM Min",
        ["col.ramRec"] = "RAM Rec",
        ["col.iops"] = "IOPS",
        ["col.latency"] = "Latency",
        ["col.category"] = "Category",
        ["col.replicas"] = "Replicas",
        ["setup.import"] = "Import Excel",
        ["status.ready"] = "Ready",
        ["status.calculated"] = "Calculated: {0} users, {1} vCPU, {2} GB RAM",
        ["status.imported"] = "Imported from Excel",
        ["status.copied"] = "Copied to clipboard",
        ["status.saved"] = "Saved to {0}",
        ["status.aiEnabled"] = "AI enabled: {0}",
        ["status.aiDisabled"] = "Rules engine",
        ["tooltip.users"] = "Number of active users (1-5000)",
        ["tooltip.deployment"] = "K8s: containers. Windows: VMs. Hybrid: both",
        ["tooltip.ha"] = "Enables 3+ nodes for high availability",
        ["tooltip.overprov"] = "Extra resource buffer (1.0-2.0x)",
        ["tooltip.calculate"] = "Run resource calculation",
        ["tooltip.langSwitch"] = "Switch language",
        ["tooltip.aiSettings"] = "Configure AI provider",
        ["matrix.info"] = "Edit the sizing matrix data. Import from Excel or edit manually.",
        ["matrix.msSql"] = "MSSQL — resource ranges",
        ["matrix.performance"] = "MSSQL — performance profile",
        ["matrix.k8sComponents"] = "K8s components",
        ["matrix.infra"] = "Infrastructure",
        ["matrix.save"] = "Save matrix",
        ["matrix.reset"] = "Reset",
        ["modules.title"] = "Modules",
        ["ai.badgeEnabled"] = "AI Online",
        ["ai.badgeDisabled"] = "Rules Engine",
        ["ai.settingsBtn"] = "AI Settings",
        ["ai.noData"] = "Run calculation to get AI recommendations",
        ["aiQuery.title"] = "AI Assistant",
        ["aiQuery.desc"] = "Describe your infrastructure in plain text. AI will analyze and fill parameters.",
        ["aiQuery.analyze"] = "Analyze",
        ["aiQuery.result"] = "Analysis Result",
        ["aiQuery.apply"] = "Apply to Calculator",
        ["aiQuery.templates"] = "Quick Templates (no AI):",
        ["aiQuery.tpl1"] = "System for 200 users with LMS and HR",
        ["aiQuery.tpl2"] = "High-load system for 1000 users",
        ["aiQuery.tpl3"] = "Minimal system for 25 users",
        ["aiQuery.offline"] = "Enable Real AI in settings for text analysis, or use templates above.",
        ["diagram.title"] = "Infrastructure Diagram",
        ["diagram.show"] = "Network Diagram",
        ["diagram.copyMermaid"] = "Copy Mermaid",
        ["dialog.matrixSaved"] = "Matrix saved",
        ["settings.ai"] = "AI Settings",
    };

    private static readonly LocalizationService _instance = new();
    public static LocalizationService Instance => _instance;

    private Dictionary<string, string> _strings;
    private string _currentLang = "uk";

    public string CurrentLang => _currentLang;
    public string Flag => _currentLang == "uk" ? "\U0001F1FA\U0001F1E6" : "\U0001F1EC\U0001F1E7";
    public string LangName => _currentLang == "uk" ? "Українська" : "English";

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationService()
    {
        _strings = StringsUk;
    }

    public void LoadLanguage(string lang)
    {
        _strings = lang == "uk" ? StringsUk : StringsEn;
        _currentLang = lang;

        OnPropertyChanged("");
        OnPropertyChanged("Item");
        OnPropertyChanged("Item[]");
        OnPropertyChanged(nameof(CurrentLang));
        OnPropertyChanged(nameof(Flag));
        OnPropertyChanged(nameof(LangName));
    }

    public string this[string key] => _strings.TryGetValue(key, out var val) ? val : $"[{key}]";

    public string Get(string key) => this[key];

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
