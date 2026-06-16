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
        ["tab.assistantTitle"] = "4. Помічник",
        ["setup.title"] = "Параметри розрахунку",
        ["setup.users"] = "Кількість користувачів:",
        ["setup.product"] = "Продукт:",
        ["setup.deployment"] = "Тип розгортання:",
        ["setup.ha"] = "Висока доступність:",
        ["setup.haHint"] = "Увімкнути HA (3+ вузли)",
        ["setup.overprov"] = "Коефіцієнт резервування:",
        ["setup.calculate"] = "Розрахувати ресурси",
        ["deploy.k8s"] = "Kubernetes",
        ["deploy.windows"] = "Windows",
        ["deploy.hybrid"] = "Гібрид (K8s + Windows)",
        ["deploy.k8sName"] = "Kubernetes",
        ["deploy.windowsName"] = "Windows",
        ["deploy.hybridName"] = "Гібрид",
        ["status.deploymentChanged"] = "Тип розгортання змінено: {0}",
        ["product.standard"] = "Стандарт",
        ["product.documentflow"] = "Документообіг",
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
        ["results.runAi"] = "🧠 Запустити AI аналіз",
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
        ["col.storageType"] = "Тип диску",
        ["col.notes"] = "Примітки",
        ["setup.import"] = "Імпорт Excel",
        ["status.ready"] = "Готово",
        ["status.calculated"] = "Розраховано: {0} користувачів, {1} vCPU, {2} ГБ RAM",
        ["status.productChanged"] = "Продукт змінено: {0}",
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
        ["matrix.k8sStandard"] = "K8s компоненти — Стандарт",
        ["matrix.k8sDocumentFlow"] = "K8s компоненти — Документообіг",
        ["matrix.infra"] = "Інфраструктура",
        ["matrix.save"] = "Зберегти матрицю",
        ["matrix.reset"] = "Скинути",
        ["modules.title"] = "Модулі",
        ["ai.badgeDisabled"] = "Правила (offline)",
        ["ai.badgeEnabled"] = "AI Online",
        ["ai.settingsBtn"] = "AI Settings",
        ["ai.noData"] = "Запустіть розрахунок для отримання рекомендацій",
        ["assistant.title"] = "Помічник",
        ["assistant.desc"] = "Опишіть вашу інфраструктуру природною мовою. Помічник проаналізує та заповнить параметри.",
        ["assistant.analyze"] = "Проаналізувати",
        ["assistant.result"] = "Результат аналізу",
        ["assistant.apply"] = "Застосувати до калькулятора",
        ["assistant.templates"] = "Швидкі шаблони:",
        ["assistant.tpl1"] = "Система на 200 користувачів з LMS та HR",
        ["assistant.tpl2"] = "Високонавантажена система на 1000 користувачів",
        ["assistant.tpl3"] = "Мінімальна система на 25 користувачів",
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
        ["tab.assistantTitle"] = "4. Assistant",
        ["setup.title"] = "Calculation Parameters",
        ["setup.users"] = "Number of Users:",
        ["setup.product"] = "Product:",
        ["setup.deployment"] = "Deployment Type:",
        ["setup.ha"] = "High Availability:",
        ["setup.haHint"] = "Enable HA (3+ nodes)",
        ["setup.overprov"] = "Overprovisioning Factor:",
        ["setup.calculate"] = "Calculate Resources",
        ["deploy.k8s"] = "Kubernetes",
        ["deploy.windows"] = "Windows",
        ["deploy.hybrid"] = "Hybrid (K8s + Windows)",
        ["deploy.k8sName"] = "Kubernetes",
        ["deploy.windowsName"] = "Windows",
        ["deploy.hybridName"] = "Hybrid",
        ["status.deploymentChanged"] = "Deployment changed: {0}",
        ["product.standard"] = "Standard",
        ["product.documentflow"] = "Document Flow",
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
        ["results.runAi"] = "\U0001F9E0 Run AI Analysis",
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
        ["col.storageType"] = "Storage Type",
        ["col.notes"] = "Notes",
        ["setup.import"] = "Import Excel",
        ["status.ready"] = "Ready",
        ["status.calculated"] = "Calculated: {0} users, {1} vCPU, {2} GB RAM",
        ["status.productChanged"] = "Product changed: {0}",
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
        ["matrix.k8sStandard"] = "K8s components — Standard",
        ["matrix.k8sDocumentFlow"] = "K8s components — Document Flow",
        ["matrix.infra"] = "Infrastructure",
        ["matrix.save"] = "Save matrix",
        ["matrix.reset"] = "Reset",
        ["modules.title"] = "Modules",
        ["ai.badgeDisabled"] = "Rules Engine",
        ["ai.badgeEnabled"] = "AI Online",
        ["ai.settingsBtn"] = "AI Settings",
        ["ai.noData"] = "Run calculation to get AI recommendations",
        ["assistant.title"] = "Assistant",
        ["assistant.desc"] = "Describe your infrastructure in plain text. The assistant will analyze and fill parameters.",
        ["assistant.analyze"] = "Analyze",
        ["assistant.result"] = "Analysis Result",
        ["assistant.apply"] = "Apply to Calculator",
        ["assistant.templates"] = "Quick Templates:",
        ["assistant.tpl1"] = "System for 200 users with LMS and HR",
        ["assistant.tpl2"] = "High-load system for 1000 users",
        ["assistant.tpl3"] = "Minimal system for 25 users",
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
