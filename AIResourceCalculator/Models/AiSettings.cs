using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIResourceCalculator.Models;

public class AiSettings
{
    public AiProvider Provider { get; set; } = AiProvider.None;
    public string ApiKey { get; set; } = "";
    public string EndpointUrl { get; set; } = "";
    public string ModelName { get; set; } = "";
    public double Temperature { get; set; } = 0.3;
    public bool EnableRealAi { get; set; } = false;

    public bool IsValid()
    {
        if (!EnableRealAi || Provider == AiProvider.None) return false;
        if (string.IsNullOrWhiteSpace(ModelName)) return false;
        return Provider switch
        {
            AiProvider.OpenAI => !string.IsNullOrWhiteSpace(ApiKey),
            AiProvider.Claude => !string.IsNullOrWhiteSpace(ApiKey),
            AiProvider.DeepSeek => !string.IsNullOrWhiteSpace(ApiKey),
            AiProvider.LocalOllama => true,
            _ => false
        };
    }

    private static readonly string ConfigPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AIResourceCalculator", "aisettings.json");

    public void Save()
    {
        var dir = System.IO.Path.GetDirectoryName(ConfigPath);
        if (dir != null && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser);
        System.IO.File.WriteAllBytes(ConfigPath, encrypted);
    }

    public static AiSettings Load()
    {
        if (System.IO.File.Exists(ConfigPath))
        {
            try
            {
                var encrypted = System.IO.File.ReadAllBytes(ConfigPath);
                var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(decrypted);
                return JsonSerializer.Deserialize<AiSettings>(json) ?? new AiSettings();
            }
            catch { }
        }
        return new AiSettings();
    }

    public string ProviderDisplay()
    {
        return Provider switch
        {
            AiProvider.OpenAI => "OpenAI",
            AiProvider.Claude => "Claude (Anthropic)",
            AiProvider.Google => "Google (Gemini)",
            AiProvider.LocalOllama => "Local (Ollama)",
            AiProvider.DeepSeek => "DeepSeek",
            _ => "Rule-based (offline)"
        };
    }

    public (string endpoint, string model) GetEndpoint()
    {
        return Provider switch
        {
            AiProvider.OpenAI => (
                string.IsNullOrEmpty(EndpointUrl) ? "https://api.openai.com/v1/chat/completions" : EndpointUrl,
                string.IsNullOrEmpty(ModelName) ? "gpt-4o-mini" : ModelName
            ),
            AiProvider.Claude => (
                string.IsNullOrEmpty(EndpointUrl) ? "https://api.anthropic.com/v1/messages" : EndpointUrl,
                string.IsNullOrEmpty(ModelName) ? "claude-3-5-haiku-20241022" : ModelName
            ),
            AiProvider.Google => (
                "https://generativelanguage.googleapis.com/v1beta",
                string.IsNullOrEmpty(ModelName) ? "gemini-1.5-flash" : ModelName
            ),
            AiProvider.LocalOllama => (
                string.IsNullOrEmpty(EndpointUrl) ? "http://localhost:11434/api/generate" : EndpointUrl,
                string.IsNullOrEmpty(ModelName) ? "llama3.2" : ModelName
            ),
            AiProvider.DeepSeek => (
                string.IsNullOrEmpty(EndpointUrl) ? "https://api.deepseek.com/v1/chat/completions" : EndpointUrl,
                string.IsNullOrEmpty(ModelName) ? "deepseek-chat" : ModelName
            ),
            _ => ("", "")
        };
    }
}
