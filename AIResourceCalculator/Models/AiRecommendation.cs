namespace AIResourceCalculator.Models;

public class AiRecommendation
{
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Severity { get; set; } = "info";
    public string Action { get; set; } = "";
    public double PotentialSavings { get; set; }

    public string DirectionEmoji => Severity switch
    {
        "critical" => "🔴",
        "warning" => "🟡",
        "info" => "💡",
        "ok" => "✅",
        _ => "ℹ️"
    };

    public string ActionPrefix => Severity switch
    {
        "critical" => "🔴 ПОТРІБНО: ",
        "warning" => "🟡 Рекомендація: ",
        "info" => "💡 Пропозиція: ",
        "ok" => "✅ Добре: ",
        _ => ""
    };

    public string ActionPrefixEn => Severity switch
    {
        "critical" => "🔴 REQUIRED: ",
        "warning" => "🟡 RECOMMENDED: ",
        "info" => "💡 SUGGESTION: ",
        "ok" => "✅ OK: ",
        _ => ""
    };
}
