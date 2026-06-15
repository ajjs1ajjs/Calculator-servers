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
    private TextBox _txtEndpoint = null!;
    private ComboBox _cmbModel = null!;
    private Slider _sliderTemp = null!;
    private TextBlock _txtTempVal = null!;
    private CheckBox _chkEnabled = null!;
    private TextBlock _txtStatus = null!;
    private StackPanel _configPanel = null!;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public AiSettingsDialog(AiSettings current)
    {
        Title = "AI Settings / Налаштування AI";
        Width = 560; Height = 520;
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

        // Title
        outer.Children.Add(new TextBlock
        {
            Text = "\u2699 AI Configuration",
            FontSize = 20, FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 15),
            Foreground = new SolidColorBrush(Color.FromRgb(0x2c, 0x3e, 0x50))
        });

        // Enable checkbox
        var headerPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        _chkEnabled = new CheckBox
        {
            Content = "Enable Real AI / Увімкнути AI",
            IsChecked = Settings.EnableRealAi,
            FontSize = 14, FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 5)
        };
        _chkEnabled.Checked += (_, _) => UpdateUi();
        _chkEnabled.Unchecked += (_, _) => UpdateUi();
        headerPanel.Children.Add(_chkEnabled);

        _txtStatus = new TextBlock
        {
            Text = "", FontSize = 12, FontWeight = FontWeights.Bold,
            Margin = new Thickness(20, 0, 0, 0)
        };
        headerPanel.Children.Add(_txtStatus);
        outer.Children.Add(headerPanel);

        // Configuration panel
        _configPanel = new StackPanel { IsEnabled = false };

        // Provider
        _configPanel.Children.Add(MakeLabel("Provider / Провайдер:"));
        _cmbProvider = new ComboBox { Margin = new Thickness(0, 2, 0, 10), Padding = new Thickness(6, 4, 6, 4), FontSize = 13 };
        _cmbProvider.Items.Add("Rule-based (offline)");
        _cmbProvider.Items.Add("OpenAI (GPT-4o, GPT-4o-mini)");
        _cmbProvider.Items.Add("Claude (Anthropic)");
        _cmbProvider.Items.Add("Local (Ollama)");
        _cmbProvider.SelectedIndex = (int)Settings.Provider;
        _cmbProvider.SelectionChanged += OnProviderChanged;
        _configPanel.Children.Add(_cmbProvider);

        // API Key
        _configPanel.Children.Add(MakeLabel("API Key:"));
        _txtApiKey = new PasswordBox
        {
            Password = Settings.ApiKey, Margin = new Thickness(0, 2, 0, 10),
            Padding = new Thickness(6, 4, 6, 4), FontSize = 13,
            ToolTip = "Required for OpenAI / Claude"
        };
        _txtApiKey.PasswordChanged += (_, _) => UpdateUi();
        _configPanel.Children.Add(_txtApiKey);

        // Endpoint
        _configPanel.Children.Add(MakeLabel("Endpoint URL:"));
        _txtEndpoint = new TextBox
        {
            Text = Settings.EndpointUrl, Margin = new Thickness(0, 2, 0, 10),
            Padding = new Thickness(6, 4, 6, 4), FontSize = 13,
            ToolTip = "Leave empty for default"
        };
        _configPanel.Children.Add(_txtEndpoint);

        // Model
        _configPanel.Children.Add(MakeLabel("Model:"));
        _cmbModel = new ComboBox
        {
            Margin = new Thickness(0, 2, 0, 10), Padding = new Thickness(6, 4, 6, 4),
            IsEditable = true, Text = Settings.ModelName, FontSize = 13
        };
        _configPanel.Children.Add(_cmbModel);

        // Temperature
        var tempPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        _sliderTemp = new Slider
        {
            Minimum = 0, Maximum = 1, Value = Settings.Temperature, Width = 200,
            TickFrequency = 0.1, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center
        };
        _sliderTemp.ValueChanged += (_, _) => _txtTempVal.Text = $"{(int)(_sliderTemp.Value * 100)}%";
        tempPanel.Children.Add(new TextBlock
        {
            Text = "Temperature:", VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0), FontSize = 13
        });
        tempPanel.Children.Add(_sliderTemp);
        _txtTempVal = new TextBlock
        {
            Text = $"{(int)(Settings.Temperature * 100)}%", VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0), FontSize = 13, FontWeight = FontWeights.Bold,
            Width = 40
        };
        tempPanel.Children.Add(_txtTempVal);
        _configPanel.Children.Add(tempPanel);

        // Test button
        var btnTest = new Button
        {
            Content = "\u26A1 Test Connection / Тест з'єднання",
            Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 5, 0, 5),
            FontSize = 13, Cursor = System.Windows.Input.Cursors.Hand,
            Background = new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB)),
            Foreground = Brushes.White, BorderThickness = new Thickness(0)
        };
        btnTest.Click += BtnTest_Click;
        _configPanel.Children.Add(btnTest);

        outer.Children.Add(_configPanel);

        // Separator
        outer.Children.Add(new Border
        {
            Height = 1, Background = new SolidColorBrush(Color.FromRgb(0xDE, 0xE2, 0xE6)),
            Margin = new Thickness(0, 5, 0, 10)
        });

        // Buttons
        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnSave = new Button
        {
            Content = "\u2714 Save / Зберегти", Width = 140, Height = 32,
            Padding = new Thickness(8, 4, 8, 4), FontSize = 13, FontWeight = FontWeights.Bold,
            IsDefault = true, Cursor = System.Windows.Input.Cursors.Hand,
            Background = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)),
            Foreground = Brushes.White, BorderThickness = new Thickness(0)
        };
        btnSave.Click += (s, e) =>
        {
            if (!Validate()) return;
            Settings.Provider = (AiProvider)_cmbProvider.SelectedIndex;
            Settings.ApiKey = _txtApiKey.Password.Trim();
            Settings.EndpointUrl = _txtEndpoint.Text.Trim();
            Settings.ModelName = _cmbModel.Text.Trim();
            Settings.Temperature = _sliderTemp.Value;
            Settings.EnableRealAi = _chkEnabled.IsChecked ?? false;
            Settings.Save();
            DialogResult = true;
            Close();
        };
        btnPanel.Children.Add(btnSave);

        var cancelBtn = new Button
        {
            Content = "Cancel / Скасувати", Width = 140, Height = 32,
            Padding = new Thickness(8, 4, 8, 4), FontSize = 13,
            IsCancel = true, Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(8, 0, 0, 0)
        };
        cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
        btnPanel.Children.Add(cancelBtn);
        outer.Children.Add(btnPanel);

        Content = outer;
        UpdateUi();
        SetModelPresets();
        Loaded += (_, _) => AutoDetectOllama();
    }

    private void OnProviderChanged(object s, SelectionChangedEventArgs e)
    {
        SetModelPresets();
        UpdateUi();
        AutoDetectOllama();
    }

    private void SetModelPresets()
    {
        _cmbModel.Items.Clear();
        var models = (AiProvider)_cmbProvider.SelectedIndex switch
        {
            AiProvider.OpenAI => new[] { "gpt-4o-mini", "gpt-4o", "gpt-4-turbo" },
            AiProvider.Claude => new[] { "claude-3-haiku-20240307", "claude-3-sonnet-20240229", "claude-3-opus-20240229" },
            AiProvider.LocalOllama => new[] { "llama3.2", "llama3.1", "mistral", "codellama", "gemma2" },
            _ => Array.Empty<string>()
        };
        foreach (var m in models) _cmbModel.Items.Add(m);
        if (models.Length > 0 && (string.IsNullOrWhiteSpace(_cmbModel.Text) || !models.Contains(_cmbModel.Text)))
            _cmbModel.Text = models[0];
    }

    private void UpdateUi()
    {
        var enabled = _chkEnabled.IsChecked ?? false;
        var provider = (AiProvider)_cmbProvider.SelectedIndex;
        var isApi = provider == AiProvider.OpenAI || provider == AiProvider.Claude;
        var isCloud = provider != AiProvider.None && provider != AiProvider.LocalOllama;

        _configPanel.IsEnabled = enabled && provider != AiProvider.None;
        _txtApiKey.Visibility = (isApi && enabled) ? Visibility.Visible : Visibility.Collapsed;
        _txtEndpoint.Visibility = (enabled && provider != AiProvider.None) ? Visibility.Visible : Visibility.Collapsed;
        _cmbModel.Visibility = (enabled && provider != AiProvider.None) ? Visibility.Visible : Visibility.Collapsed;
        _sliderTemp.Visibility = (enabled && isCloud) ? Visibility.Visible : Visibility.Collapsed;

        var hasKey = !string.IsNullOrWhiteSpace(_txtApiKey.Password);
        if (!enabled) _txtStatus.Text = "\u23F8 AI disabled / AI вимкнено";
        else if (provider == AiProvider.None) _txtStatus.Text = "\u23F8 Rule-based mode / Локальні правила";
        else if (provider == AiProvider.LocalOllama) _txtStatus.Text = "\u23F3 Local Ollama mode";
        else if (isApi && !hasKey) _txtStatus.Text = "\u26A0 API Key required / Потрібен ключ";
        else _txtStatus.Text = "\u2714 Ready / Готово";
    }

    private bool Validate()
    {
        if (!(_chkEnabled.IsChecked ?? false)) return true;
        var provider = (AiProvider)_cmbProvider.SelectedIndex;
        if (provider == AiProvider.None) return true;

        if ((provider == AiProvider.OpenAI || provider == AiProvider.Claude) &&
            string.IsNullOrWhiteSpace(_txtApiKey.Password))
        {
            MessageBox.Show("API Key is required for this provider.\nПотрібен API ключ.", "Validation / Валідація",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            _txtApiKey.Focus();
            return false;
        }
        if (string.IsNullOrWhiteSpace(_cmbModel.Text))
        {
            MessageBox.Show("Model name is required.\nВкажіть назву моделі.", "Validation / Валідація",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            _cmbModel.Focus();
            return false;
        }
        return true;
    }

    private async void BtnTest_Click(object sender, RoutedEventArgs e)
    {
        var provider = (AiProvider)_cmbProvider.SelectedIndex;
        _txtStatus.Text = "\u23F3 Testing... / Тестування...";
        _txtStatus.Foreground = Brushes.Gray;

        try
        {
            if (provider == AiProvider.LocalOllama)
            {
                var ep = string.IsNullOrWhiteSpace(_txtEndpoint.Text)
                    ? "http://localhost:11434" : _txtEndpoint.Text.Trim().TrimEnd('/');
                var resp = await _http.GetAsync($"{ep}/api/tags");
                _txtStatus.Text = resp.IsSuccessStatusCode ? "\u2714 Ollama connected / Під'єднано" : "\u2717 Not responding / Немає відповіді";
                _txtStatus.Foreground = resp.IsSuccessStatusCode ? Brushes.Green : Brushes.Red;
            }
            else if (provider == AiProvider.OpenAI)
            {
                var ep = string.IsNullOrWhiteSpace(_txtEndpoint.Text)
                    ? "https://api.openai.com/v1/models" : _txtEndpoint.Text.Trim().TrimEnd('/') + "/models";
                var key = _txtApiKey.Password.Trim();
                if (string.IsNullOrWhiteSpace(key)) { _txtStatus.Text = "\u2717 Enter API Key first / Введіть ключ"; _txtStatus.Foreground = Brushes.Red; return; }

                var req = new HttpRequestMessage(HttpMethod.Get, ep);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
                var resp = await _http.SendAsync(req);
                _txtStatus.Text = resp.IsSuccessStatusCode ? "\u2714 OpenAI connected / Під'єднано" : "\u2717 Invalid API Key / Невірний ключ";
                _txtStatus.Foreground = resp.IsSuccessStatusCode ? Brushes.Green : Brushes.Red;
            }
            else if (provider == AiProvider.Claude)
            {
                var ep = string.IsNullOrWhiteSpace(_txtEndpoint.Text)
                    ? "https://api.anthropic.com/v1/messages" : _txtEndpoint.Text.Trim();
                var key = _txtApiKey.Password.Trim();
                if (string.IsNullOrWhiteSpace(key)) { _txtStatus.Text = "\u2717 Enter API Key first / Введіть ключ"; _txtStatus.Foreground = Brushes.Red; return; }

                var req = new HttpRequestMessage(HttpMethod.Post, ep);
                req.Headers.Add("x-api-key", key);
                req.Headers.Add("anthropic-version", "2023-06-01");
                req.Content = new StringContent(
                    JsonSerializer.Serialize(new { model = "claude-3-haiku-20240307", max_tokens = 10, messages = new[] { new { role = "user", content = "hi" } } }),
                    System.Text.Encoding.UTF8, "application/json");
                var resp = await _http.SendAsync(req);
                _txtStatus.Text = resp.IsSuccessStatusCode ? "\u2714 Claude connected / Під'єднано" : "\u2717 Invalid Key / Невірний ключ";
                _txtStatus.Foreground = resp.IsSuccessStatusCode ? Brushes.Green : Brushes.Red;
            }
        }
        catch (Exception ex)
        {
            _txtStatus.Text = $"\u2717 Error: {ex.Message}";
            _txtStatus.Foreground = Brushes.Red;
        }
    }

    private async void AutoDetectOllama()
    {
        if ((AiProvider)_cmbProvider.SelectedIndex != AiProvider.LocalOllama) return;
        if (!(_chkEnabled.IsChecked ?? false)) return;

        try
        {
            var ep = string.IsNullOrWhiteSpace(_txtEndpoint.Text)
                ? "http://localhost:11434" : _txtEndpoint.Text.Trim().TrimEnd('/');
            var resp = await _http.GetAsync($"{ep}/api/tags");
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var models = doc.RootElement.GetProperty("models").EnumerateArray()
                    .Select(m => m.GetProperty("name").GetString() ?? "").Where(m => !string.IsNullOrEmpty(m)).ToList();

                if (models.Count > 0)
                {
                    _cmbModel.Items.Clear();
                    foreach (var m in models) _cmbModel.Items.Add(m);
                    _cmbModel.Text = models[0];
                    _txtStatus.Text = $"\u2714 Found {models.Count} model(s) / Знайдено {models.Count} моделей";
                    _txtStatus.Foreground = Brushes.Green;
                }
            }
        }
        catch (HttpRequestException)
        {
            if (_cmbModel.Items.Count == 0)
            {
                foreach (var m in new[] { "llama3.2", "llama3.1", "mistral" }) _cmbModel.Items.Add(m);
                _cmbModel.Text = "llama3.2";
            }
            _txtStatus.Text = "\u26A0 Ollama not found / Не знайдено";
            _txtStatus.Foreground = Brushes.Orange;
        }
        catch { }
    }

    private static TextBlock MakeLabel(string text) => new()
    {
        Text = text, FontSize = 13, FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 5, 0, 2),
        Foreground = new SolidColorBrush(Color.FromRgb(0x2c, 0x3e, 0x50))
    };
}
