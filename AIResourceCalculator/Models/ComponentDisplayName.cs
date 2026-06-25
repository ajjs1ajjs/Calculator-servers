namespace AIResourceCalculator.Models;

// Українські назви компонентів (подів) для відображення у звітах та UI. Ключ — канонічна
// (англ./технічна) назва компонента з матриці; вона лишається стабільною для логіки та імпорту,
// а у звітах показується локалізований варіант. Якщо назви немає в мапі — повертається як є
// (напр. для імпортованих користувачем компонентів).
public static class ComponentDisplayName
{
    private static readonly Dictionary<string, string> Uk = new()
    {
        // App Server
        ["AS (App Server)"] = "AS (сервер додатків)",
        ["AS-Local SQL"] = "AS — локальний SQL",
        ["AS-Redis"] = "AS — Redis (кеш)",
        // ROBOT
        ["ROBOT"] = "ROBOT (планувальник)",
        ["ROBOT-Local SQL"] = "ROBOT — локальний SQL",
        ["ROBOT-Redis"] = "ROBOT — Redis (кеш)",
        // Web
        ["Webrmd"] = "Webrmd (веб-клієнт)",
        ["SmartID"] = "SmartID (єдиний вхід)",
        ["WS (WebSocket)"] = "WS (веб-сокет)",
        ["WS-SignalR"] = "WS-SignalR (сповіщення)",
        // ForceBPM
        ["GraphQL"] = "GraphQL (шлюз даних)",
        ["ForceBPM Engine"] = "ForceBPM — рушій",
        ["ForceBPM Modeler"] = "ForceBPM — конструктор",
        ["ForceBPM Processes"] = "ForceBPM — процеси",
        ["ForceBPM Tasks"] = "ForceBPM — завдання",
        ["ForceBPM Tasks-Graphql"] = "ForceBPM — завдання (GraphQL)",
        // LMS (навчання)
        ["LMS-SmartID"] = "LMS — SmartID",
        ["LMS"] = "LMS (навчання)",
        ["LMS-GraphQL"] = "LMS — GraphQL",
        ["LMS-Videoutilities"] = "LMS — відеоутиліти",
        ["LMS-Fileserver"] = "LMS — файловий сервер",
        // HR Portal
        ["HR-SmartID"] = "HR-портал — SmartID",
        ["HR-GraphQL"] = "HR-портал — GraphQL",
        ["WebAppModeler"] = "HR-портал — конструктор застосунків",
        ["CommonAppPlayer"] = "HR-портал — програвач застосунків",
    };

    public static string Localize(string name)
        => name != null && Uk.TryGetValue(name, out var loc) ? loc : name ?? "";
}
