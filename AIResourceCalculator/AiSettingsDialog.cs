using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AIResourceCalculator.Models;

namespace AIResourceCalculator;

public class AiSettingsDialog : Window
{
    public AiSettings Settings { get; private set; }

    private ComboBox _cmbProvider = null!;
    private PasswordBox _txtApiKey = null!;
    private ComboBox _cmbModel = null!;
    private CheckBox _chkEnabled = null!;
    private TextBlock _txtStatus = null!;
    private Button _btnFetch = null!;
    private StackPanel _configPanel = null!;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public AiSettingsDialog(AiSettings current)
    {
        Title = "AI Settings";
        Width = 520; Height = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xF9, 0xFA));

        Settings = new AiSettings
        {
            Provider = current.Provider,
            ApiKey = current.ApiKey,
            EndpointUrl = current.EndpointUrl,
            ModelName = current.ModelName,
            Temperature = current.Temperature,
            EnableRealAi = current.EnableRealAi
        };

        var outer = new StackPanel { Margin = new Thickness(20, 15, 20, 15) };

        outer.Children.Add(new TextBlock
        {
            Text = "AI Configuration",
            FontSize = 20, FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 15),
            Foreground = new SolidColorBrush(Color.FromRgb(0x2c, 0x3e, 0x50))
        });

        var headerPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        _chkEnabled = new CheckBox
        {
            Content = "Enable Real AI",
            IsChecked = Settings.EnableRealAi,
            FontSize = 14, FontWeight = FontWeights.Bold
        };
        _chkEnabled.Checked += (_, _) => OnEnabledChanged();
        _chkEnabled.Unchecked += (_, _) => UpdateUi();
        headerPanel.Children.Add(_chkEnabled);

        _txtStatus = new TextBlock
        {
            Text = "", FontSize = 12, FontWeight = FontWeights.Bold,
            Margin = new Thickness(20, 2, 0, 0)
        };
        headerPanel.Children.Add(_txtStatus);
        outer.Children.Add(headerPanel);

        _configPanel = new StackPanel { IsEnabled = false };

        _configPanel.Children.Add(MakeLabel("Provider:"));
        _cmbProvider = new ComboBox { Margin = new Thickness(0, 2, 0, 10), Padding = new Thickness(6, 4, 6, 4), FontSize = 13 };
        _cmbProvider.Items.Add("OpenAI (GPT-4o, GPT-4o-mini)");
        _cmbProvider.Items.Add("Claude (Anthropic)");
        _cmbProvider.Items.Add("Google (Gemini)");
        _cmbProvider.Items.Add("Local (Ollama)");
        _cmbProvider.Items.Add("DeepSeek");
        _cmbProvider.Items.Add("OpenCode (OpenAI-compatible)");
        _cmbProvider.SelectedIndex = Settings.Provider == AiProvider.None ? 0 : Math.Max(0, (int)Settings.Provider - 1);
        _cmbProvider.SelectionChanged += (_, _) => UpdateUi();
        _configPanel.Children.Add(_cmbProvider);

        _configPanel.Children.Add(MakeLabel("API Key:"));
        var keyPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 5) };
        _txtApiKey = new PasswordBox
        {
            Password = Settings.ApiKey, Width = 300, Padding = new Thickness(6, 4, 6, 4), FontSize = 13
        };
        _txtApiKey.PasswordChanged += (_, _) => UpdateUi();
        keyPanel.Children.Add(_txtApiKey);

        _btnFetch = new Button
        {
            Content = "Fetch Models",
            Width = 100, Height = 26, Margin = new Thickness(8, 0, 0, 0), FontSize = 12,
            Background = new SolidColorBrush(Color.FromRgb(0x1e, 0x66, 0xf5)),
            Foreground = Brushes.White, BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center, Cursor = System.Windows.Input.Cursors.Hand
        };
        _btnFetch.Click += BtnFetch_Click;
        keyPanel.Children.Add(_btnFetch);
        _configPanel.Children.Add(keyPanel);

        _configPanel.Children.Add(MakeLabel("Model:"));
        _cmbModel = new ComboBox
        {
            Margin = new Thickness(0, 2, 0, 10), Padding = new Thickness(6, 4, 6, 4),
            IsEditable = true, Text = Settings.ModelName, FontSize = 13
        };
        _configPanel.Children.Add(_cmbModel);

        outer.Children.Add(_configPanel);

        outer.Children.Add(new Border
        {
            Height = 1, Background = new SolidColorBrush(Color.FromRgb(0xDE, 0xE2, 0xE6)),
            Margin = new Thickness(0, 5, 0, 10)
        });

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnSave = new Button
        {
            Content = "Save", Width = 120, Height = 32, FontSize = 13, FontWeight = FontWeights.Bold,
            IsDefault = true, Cursor = System.Windows.Input.Cursors.Hand,
            Background = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)),
            Foreground = Brushes.White, BorderThickness = new Thickness(0)
        };
        btnSave.Click += (s, e) =>
        {
            Settings.Provider = _cmbProvider.SelectedIndex switch
            {
                0 => AiProvider.OpenAI, 1 => AiProvider.Claude, 2 => AiProvider.Google,
                3 => AiProvider.LocalOllama, 4 => AiProvider.DeepSeek, 5 => AiProvider.OpenCode,
                _ => AiProvider.None
            };
            Settings.ApiKey = _txtApiKey.Password.Trim();
            Settings.ModelName = _cmbModel.Text.Trim();
            Settings.EnableRealAi = _chkEnabled.IsChecked ?? false;
            Settings.Temperature = 0.3;
            Settings.Save();
            DialogResult = true;
            Close();
        };
        btnPanel.Children.Add(btnSave);

        var cancelBtn = new Button
        {
            Content = "Cancel", Width = 100, Height = 32, FontSize = 13,
            IsCancel = true, Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(8, 0, 0, 0)
        };
        cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
        btnPanel.Children.Add(cancelBtn);
        outer.Children.Add(btnPanel);

        Content = outer;
        UpdateUi();
        Loaded += (_, _) => UpdateUi();
    }

    private void OnEnabledChanged()
    {
        if (_cmbProvider.SelectedIndex < 0) _cmbProvider.SelectedIndex = 0;
        UpdateUi();
    }

    private async void BtnFetch_Click(object sender, RoutedEventArgs e)
    {
        _txtStatus.Text = "Fetching models...";
        _txtStatus.Foreground = Brushes.Gray;
        _btnFetch.IsEnabled = false;
        _cmbModel.Items.Clear();

        int idx = _cmbProvider.SelectedIndex;

        if (idx == 3)
        {
            await AutoDetectOllamaAsync();
            _btnFetch.IsEnabled = true;
            return;
        }

        var key = _txtApiKey.Password.Trim();
        if (string.IsNullOrEmpty(key))
        {
            _txtStatus.Text = "Enter API Key first";
            _txtStatus.Foreground = Brushes.Red;
            _btnFetch.IsEnabled = true;
            return;
        }

        try
        {
            if (idx == 0) await FetchOpenAiModels(key);
            else if (idx == 1) await FetchClaudeModels(key);
            else if (idx == 2) await FetchGoogleModels(key);
            else if (idx == 4) await FetchDeepSeekModels(key);
            else if (idx == 5) await FetchFallbackModels();
            _txtStatus.Text = $"Loaded {_cmbModel.Items.Count} models";
            _txtStatus.Foreground = Brushes.Green;
        }
        catch (Exception ex)
        {
            _txtStatus.Text = $"Error: {ex.Message}";
            _txtStatus.Foreground = Brushes.Red;
        }
        _btnFetch.IsEnabled = true;
    }

    private async Task FetchOpenAiModels(string key)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) throw new Exception("Invalid key or network error");

        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var models = doc.RootElement.GetProperty("data").EnumerateArray()
            .Select(m => m.GetProperty("id").GetString() ?? "")
            .Where(id => id.StartsWith("gpt-") && !id.Contains("instruct") && !id.Contains("audio") && !id.Contains("realtime"))
            .OrderByDescending(id => id)
            .ToList();

        _cmbModel.Items.Clear();
        foreach (var m in models) _cmbModel.Items.Add(m);
        if (models.Count > 0) _cmbModel.Text = models.Contains("gpt-4o-mini") ? "gpt-4o-mini" : models[0];
    }

    private async Task FetchClaudeModels(string key)
    {
        var models = new[] { "claude-3-opus-20240229", "claude-3-sonnet-20240229", "claude-3-haiku-20240307", "claude-3-5-sonnet-20241022" };

        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", key);
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Content = new StringContent(
            JsonSerializer.Serialize(new { model = "claude-3-haiku-20240307", max_tokens = 5, messages = new[] { new { role = "user", content = "ok" } } }),
            System.Text.Encoding.UTF8, "application/json");

        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) throw new Exception("Invalid key or network error");

        _cmbModel.Items.Clear();
        foreach (var m in models) _cmbModel.Items.Add(m);
        _cmbModel.Text = "claude-3-haiku-20240307";
    }

    private async Task FetchGoogleModels(string key)
    {
        var resp = await _http.GetAsync($"https://generativelanguage.googleapis.com/v1beta/models?key={key}");
        if (!resp.IsSuccessStatusCode) throw new Exception("Invalid key or network error");

        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var models = doc.RootElement.GetProperty("models").EnumerateArray()
            .Select(m => m.GetProperty("name").GetString()?.Replace("models/", "") ?? "")
            .Where(n => n.Contains("gemini") && !n.Contains("embedding"))
            .ToList();

        _cmbModel.Items.Clear();
        foreach (var m in models) _cmbModel.Items.Add(m);
        if (models.Count > 0) _cmbModel.Text = models.Contains("gemini-1.5-flash") ? "gemini-1.5-flash" : models[0];
    }

    private async Task FetchDeepSeekModels(string key)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "https://api.deepseek.com/v1/models");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) throw new Exception("Invalid key or network error");

        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var models = doc.RootElement.GetProperty("data").EnumerateArray()
            .Select(m => m.GetProperty("id").GetString() ?? "")
            .Where(id => !string.IsNullOrEmpty(id))
            .OrderByDescending(id => id)
            .ToList();

        _cmbModel.Items.Clear();
        foreach (var m in models) _cmbModel.Items.Add(m);
        if (models.Count > 0) _cmbModel.Text = models.Contains("deepseek-chat") ? "deepseek-chat" : models[0];
    }

    private async Task AutoDetectOllamaAsync()
    {
        try
        {
            _txtStatus.Text = "Connecting to Ollama...";
            _txtStatus.Foreground = Brushes.Gray;

            var resp = await _http.GetAsync("http://localhost:11434/api/tags");
            if (!resp.IsSuccessStatusCode)
            {
                throw new Exception($"Server returned {resp.StatusCode}");
            }

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var models = doc.RootElement.GetProperty("models").EnumerateArray()
                .Select(m => m.GetProperty("name").GetString() ?? "")
                .Where(m => !string.IsNullOrEmpty(m))
                .ToList();

            if (models.Count == 0)
            {
                _txtStatus.Text = "No models found. Pull one: ollama pull llama3.2";
                _txtStatus.Foreground = Brushes.Orange;
                AddFallbackModels();
                return;
            }

            _cmbModel.Items.Clear();
            foreach (var m in models) _cmbModel.Items.Add(m);
            _cmbModel.Text = models[0];
            _txtStatus.Text = $"Found {models.Count} local model(s)";
            _txtStatus.Foreground = Brushes.Green;
        }
        catch (HttpRequestException)
        {
            _txtStatus.Text = "Ollama not running. Start it: ollama serve";
            _txtStatus.Foreground = Brushes.Orange;
            AddFallbackModels();
        }
        catch (Exception ex)
        {
            _txtStatus.Text = $"Error: {ex.Message}";
            _txtStatus.Foreground = Brushes.Red;
            AddFallbackModels();
        }
    }

    private async Task FetchFallbackModels()
    {
        _txtStatus.Text = "OpenCode: set custom endpoint URL in settings";
        _txtStatus.Foreground = Brushes.Gray;
        _cmbModel.Items.Clear();
        foreach (var m in new[] { "custom-model", "gpt-4o-mini", "deepseek-chat", "llama3.2" })
            _cmbModel.Items.Add(m);
        _cmbModel.Text = "custom-model";
        await Task.CompletedTask;
    }

    private void AddFallbackModels()
    {
        if (_cmbModel.Items.Count > 0) return;
        foreach (var m in new[] { "llama3.2", "llama3.1", "mistral", "codellama", "gemma2" })
            _cmbModel.Items.Add(m);
        if (string.IsNullOrEmpty(_cmbModel.Text))
            _cmbModel.Text = "llama3.2";
    }

    private void UpdateUi()
    {
        var enabled = _chkEnabled.IsChecked ?? false;
        var idx = _cmbProvider.SelectedIndex;
        var isCloud = idx >= 0 && idx <= 2 || idx >= 4;

        _configPanel.IsEnabled = enabled;
        _txtApiKey.Visibility = (enabled && isCloud) ? Visibility.Visible : Visibility.Collapsed;
        _btnFetch.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        _cmbModel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;

        if (!enabled) _txtStatus.Text = "AI disabled";
        else if (idx == 3) _txtStatus.Text = "Click Fetch Models to detect local models";
        else if (idx == 5) _txtStatus.Text = "Enter endpoint URL and API key (if required), then click Fetch Models";
        else if (string.IsNullOrWhiteSpace(_txtApiKey.Password)) _txtStatus.Text = "Paste API key, then click Fetch Models";
        else _txtStatus.Text = "Click Fetch Models to load available models";
    }

    private static TextBlock MakeLabel(string text) => new()
    {
        Text = text, FontSize = 13, FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 5, 0, 2),
        Foreground = new SolidColorBrush(Color.FromRgb(0x2c, 0x3e, 0x50))
    };
}
