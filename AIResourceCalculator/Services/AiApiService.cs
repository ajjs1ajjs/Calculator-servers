using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

public class AiApiService
{
    private readonly HttpClient _http;
    private readonly AiSettings _settings;

    public AiApiService(AiSettings settings)
    {
        _settings = settings;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
    }

    public async Task<string?> GetRecommendation(string prompt)
    {
        if (!_settings.EnableRealAi || _settings.Provider == AiProvider.None)
            return null;

        try
        {
            var (endpoint, model) = _settings.GetEndpoint();

            return _settings.Provider switch
            {
                AiProvider.OpenAI => await CallOpenAi(endpoint, model, prompt),
                AiProvider.Claude => await CallClaude(endpoint, model, prompt),
                AiProvider.Google => await CallGoogle(endpoint, model, prompt),
                AiProvider.LocalOllama => await CallOllama(endpoint, model, prompt),
                _ => null
            };
        }
        catch (TaskCanceledException)
        {
            return "Помилка: перевищено час очікування (120 с). Перевірте з'єднання з API або спробуйте ще раз.";
        }
        catch (HttpRequestException ex)
        {
            return $"Помилка мережі: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Помилка AI: {ex.Message}";
        }
    }

    private async Task<string> CallOpenAi(string endpoint, string model, string prompt)
    {
        var body = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = "You are an infrastructure architect. Give concise resource recommendations in JSON format." },
                new { role = "user", content = prompt }
            },
            temperature = _settings.Temperature,
            max_tokens = 800
        };

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0 &&
            choices[0].TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var content))
            return content.GetString() ?? "";
        return "";
    }

    private async Task<string> CallClaude(string endpoint, string model, string prompt)
    {
        var body = new
        {
            model,
            max_tokens = 800,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("x-api-key", _settings.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("content", out var contentArr) && contentArr.ValueKind == JsonValueKind.Array && contentArr.GetArrayLength() > 0 &&
            contentArr[0].TryGetProperty("text", out var text))
            return text.GetString() ?? "";
        return "";
    }

    private async Task<string> CallOllama(string endpoint, string model, string prompt)
    {
        // Quick health check
        var baseUri = new Uri(endpoint);
        var tagsUri = new Uri(baseUri, "/api/tags");
        var baseUrl = tagsUri.ToString();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var ping = await _http.GetAsync(baseUrl, cts.Token);
            if (!ping.IsSuccessStatusCode)
                return "⚠️ Ollama сервер недоступний. Переконайтесь, що Ollama запущена (ollama serve).";
        }
        catch
        {
            return "⚠️ Ollama сервер не відповідає. Запустіть Ollama та перевірте http://localhost:11434";
        }

        var body = new
        {
            model,
            prompt = $"You are an infrastructure architect. Give concise resource recommendations.\n\n{prompt}",
            stream = false,
            options = new { temperature = _settings.Temperature }
        };

        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(endpoint, content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("response", out var resp))
            return resp.GetString() ?? "";
        return "";
    }

    private async Task<string?> CallGoogle(string endpoint, string model, string prompt)
    {
        var url = $"{endpoint}/models/{model}:generateContent?key={_settings.ApiKey}";
        var body = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } }
        };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(url, content);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var text = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            return text ?? "";
        }
        return "";
    }

    public string BuildAnalysisPrompt(ResourceRequirement req, ProjectConfig config)
    {
        var infra = string.Join("\n", req.Infrastructure.Select(i =>
            $"  - {i.Name}: {i.NodeCount} nodes, {i.Cpu} vCPU, {i.RamGb} GB RAM, {i.StorageGb} GB storage"));

        var components = string.Join("\n", req.Components.Where(c => c.Cpu > 0).Select(c =>
            $"  - {c.Name}: {c.Cpu} vCPU, {c.RamGb} GB RAM, {c.Replicas} replicas"));

        return $@"[System: You are an infrastructure sizing expert. Use AI models and best practices for optimal resource calculation.]

Analyze this infrastructure configuration:

Project: {config.ProjectName}
Users: {config.UserCount}
Deployment: {config.DeploymentType} ({config.LoadProfile})
HA: {config.HaEnabled}

Totals: vCPU={req.TotalCpu:F1}, RAM={req.TotalRamGb:F1} GB, Storage={req.TotalStorageGb} GB, IOPS={req.TotalIops}

Infrastructure:
{infra}

Components:
{components}

Provide 3-5 specific recommendations in JSON: category, title, description, action, severity (ok/warning/critical), potentialSavings ($/month).
Focus on: instance sizing, CPU/RAM balance, HA scaling, storage optimization. Use Ukrainian language.";
    }
}
