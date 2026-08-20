namespace ResourceCalculator.Models;

// Назви компонентів (подів) для відображення у звітах та UI. Беруться ДОСЛІВНО з еталонних
// розрахунків (Калькулятор розрахунку ресурсів / D-AD-ADM-E), а НЕ перекладаються самочинно.
// Більшість назв — канонічні технічні (AS-Local SQL, ForceBPM Engine, WebAppModeler …) і
// повертаються як є. Лише кілька в еталоні мають український уточнювач — їх задаємо тут точно
// так, як у файлах (з великої літери, без власних варіацій).
public static class ComponentDisplayName
{
    private static readonly Dictionary<string, string> Uk = new()
    {
        // Уточнювачі — дослівно з еталонних таблиць розрахунків.
        ["AS (App Server)"] = "AS (Сервер додатків)",
        ["ROBOT"] = "ROBOT (Планувальник)",
        ["Webrmd"] = "Webrmd (Веб клієнт)",
        // Решта (AS-Local SQL, AS-Redis, ROBOT-Local SQL, ROBOT-Redis, SmartID, WS (WebSocket),
        // WS-SignalR, GraphQL, ForceBPM *, LMS *, WebAppModeler, CommonAppPlayer …) — без змін:
        // показуються канонічними технічними назвами через fallback нижче.
    };

    public static string Localize(string name)
        => name != null && Uk.TryGetValue(name, out var loc) ? loc : name ?? "";
}
