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

    private ComboBox _cmbProvider;
    private PasswordBox _txtApiKey;
    private TextBox _txtEndpoint;
    private ComboBox _cmbModel;
    private Slider _sliderTemp;
    private CheckBox _chkEnabled;
    private TextBlock _txtStatus;
    private Button _btnTest;
    private Button _btnSave;
    private StackPanel _modelPanel;
    private StackPanel _apiKeyPanel;
    private StackPanel _endpointPanel;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public AiSettingsDialog(AiSettings current)
    {
        Title = "AI Settings / Налаштування AI";
        Width = 560; Height = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        Settings = new AiSettings
        {
            Provider = current.Provider,
            ApiKey = current.ApiKey,
            EndpointUrl = current.EndpointUrl,
            ModelName = current.ModelName,
            Temperature = current.Temperature,
            EnableRealAi = current.EnableRealAi
        };

        var grid = new Grid { Margin = new Thickness(15) };
        for (int i = 0; i < 12; i++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        int row = 0;

        // Чекбокс увімкнення AI
        _chkEnabled = new CheckBox
        {
            Content = "Enable Real AI / Увімкнути AI",
            IsChecked = Settings.EnableRealAi,
            Margin = new Thickness(5),
            FontSize = 14,
            FontWeight = FontWeights.Bold
        };
        _chkEnabled.Checked += (_, _) => UpdateUi();
        _chkEnabled.Unchecked += (_, _) => UpdateUi();
        Grid.SetRow(_chkEnabled, row); Grid.SetColumnSpan(_chkEnabled, 2);
        grid.Children.Add(_chkEnabled);
        row++;

        // Вибір провайдера
        AddLabel(grid, row, 0, "Provider:");
        _cmbProvider = new ComboBox
        {
            Margin = new Thickness(5), Padding = new Thickness(4),
            SelectedIndex = (int)Settings.Provider
        };
        _cmbProvider.Items.Add("Rule-based (offline)");
        _cmbProvider.Items.Add("OpenAI");
        _cmbProvider.Items.Add("Claude (Anthropic)");
        _cmbProvider.Items.Add("Local (Ollama)");
        _cmbProvider.SelectionChanged += (_, _) => { UpdateUi(); AutoDetectOllama(); };
        Grid.SetRow(_cmbProvider, row); Grid.SetColumn(_cmbProvider, 1);
        grid.Children.Add(_cmbProvider);
        row++;

        // Панель API ключа
        _apiKeyPanel = new StackPanel { Margin = new Thickness(0) };
        AddLabel(_apiKeyPanel, 0, 0, "API Key:");
        _txtApiKey = new PasswordBox
        {
            Password = Settings.ApiKey, Margin = new Thickness(5), Padding = new Thickness(4),
            ToolTip = "Required for OpenAI / Claude"
        };
        _txtApiKey.PasswordChanged += (_, _) => UpdateUi();
        Grid.SetRow(_txtApiKey, 0); Grid.SetColumn(_txtApiKey, 1);
        _apiKeyPanel.Children.Add(_txtApiKey);
        Grid.SetRow(_apiKeyPanel, row); Grid.SetColumnSpan(_apiKeyPanel, 2);
        grid.Children.Add(_apiKeyPanel);
        row++;

        // Панель адреси endpoint
        _endpointPanel = new StackPanel { Margin = new Thickness(0) };
        AddLabel(_endpointPanel, 0, 0, "Endpoint URL:");
        _txtEndpoint = new TextBox
        {
            Text = Settings.EndpointUrl, Margin = new Thickness(5), Padding = new Thickness(4),
            ToolTip = "Leave empty for default"
        };
        Grid.SetRow(_txtEndpoint, 0); Grid.SetColumn(_txtEndpoint, 1);
        _endpointPanel.Children.Add(_txtEndpoint);
        Grid.SetRow(_endpointPanel, row); Grid.SetColumnSpan(_endpointPanel, 2);
        grid.Children.Add(_endpointPanel);
        row++;

        // Панель моделі
        _modelPanel = new StackPanel { Margin = new Thickness(0) };
        AddLabel(_modelPanel, 0, 0, "Model:");
        _cmbModel = new ComboBox
        {
            Margin = new Thickness(5), Padding = new Thickness(4),
            IsEditable = true, Text = Settings.ModelName
        };
        Grid.SetRow(_cmbModel, 0); Grid.SetColumn(_cmbModel, 1);
        _modelPanel.Children.Add(_cmbModel);
        Grid.SetRow(_modelPanel, row); Grid.SetColumnSpan(_modelPanel, 2);
        grid.Children.Add(_modelPanel);
        row++;

        // Температура
        AddLabel(grid, row, 0, "Temperature:");
        _sliderTemp = new Slider
        {
            Minimum = 0, Maximum = 1, Value = Settings.Temperature,
            TickFrequency = 0.1, IsSnapToTickEnabled = true, Margin = new Thickness(5)
        };
        Grid.SetRow(_sliderTemp, row); Grid.SetColumn(_sliderTemp, 1);
        grid.Children.Add(_sliderTemp);
        row++;

        // Кнопка тесту з'єднання + статус
        var testPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
        _btnTest = new Button
        {
            Content = "Test Connection / Тест", Width = 160, Height = 28,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        _btnTest.Click += BtnTest_Click;
        testPanel.Children.Add(_btnTest);

        _txtStatus = new TextBlock
        {
            Text = "", Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.Bold
        };
        testPanel.Children.Add(_txtStatus);
        Grid.SetRow(testPanel, row); Grid.SetColumnSpan(testPanel, 2);
        grid.Children.Add(testPanel);
        row++;

        // Підказки
        var hints = new TextBlock
        {
            Text = "💡 OpenAI: gpt-4o-mini | Claude: claude-3-haiku | Ollama: needs server running",
            FontSize = 11, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(5)
        };
        Grid.SetRow(hints, row); Grid.SetColumnSpan(hints, 2);
        grid.Children.Add(hints);
        row++;

        // Кнопки
        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };

        _btnSave = new Button
        {
            Content = "Save / Зберегти", Width = 130, Height = 30,
            Padding = new Thickness(6), Cursor = System.Windows.Input.Cursors.Hand,
            IsDefault = true
        };
        _btnSave.Click += (s, e) =>
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
        btnPanel.Children.Add(_btnSave);

        var cancelBtn = new Button
        {
            Content = "Cancel / Скасувати", Width = 130, Height = 30,
            Padding = new Thickness(6), Cursor = System.Windows.Input.Cursors.Hand,
            IsCancel = true
        };
        cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
        btnPanel.Children.Add(cancelBtn);

        Grid.SetRow(btnPanel, row); Grid.SetColumnSpan(btnPanel, 2);
        grid.Children.Add(btnPanel);

        Content = grid;
        UpdateUi();
        Loaded += (_, _) => AutoDetectOllama();
    }

    private void UpdateUi()
    {
        var enabled = _chkEnabled.IsChecked ?? false;
        var provider = (AiProvider)_cmbProvider.SelectedIndex;
        var isApi = provider == AiProvider.OpenAI || provider == AiProvider.Claude;

        _txtApiKey.IsEnabled = enabled && isApi;
        _txtEndpoint.IsEnabled = enabled && provider != AiProvider.None;
        _cmbModel.IsEnabled = enabled && provider != AiProvider.None;
        _sliderTemp.IsEnabled = enabled && provider != AiProvider.None;
        _btnTest.IsEnabled = enabled && provider != AiProvider.None;
        _btnSave.IsEnabled = enabled;

        _apiKeyPanel.Visibility = isApi && enabled ? Visibility.Visible : Visibility.Collapsed;
        _endpointPanel.Visibility = provider != AiProvider.None && enabled ? Visibility.Visible : Visibility.Collapsed;
        _modelPanel.Visibility = provider != AiProvider.None && enabled ? Visibility.Visible : Visibility.Collapsed;

        var hasKey = !string.IsNullOrWhiteSpace(_txtApiKey.Password);
        var hasModel = !string.IsNullOrWhiteSpace(_cmbModel.Text);

        if (!enabled) _txtStatus.Text = "⏸ AI disabled";
        else if (provider == AiProvider.None) _txtStatus.Text = "⏸ Rule-based mode";
        else if (provider == AiProvider.LocalOllama) _txtStatus.Text = "⏳ Check Ollama...";
        else if (isApi && !hasKey) _txtStatus.Text = "⚠️ API Key required";
        else if (!hasModel) _txtStatus.Text = "⚠️ Model required";
        else _txtStatus.Text = "✓ Configured";
    }

    private bool Validate()
    {
        if (!(_chkEnabled.IsChecked ?? false)) return true;

        var provider = (AiProvider)_cmbProvider.SelectedIndex;
        if (provider == AiProvider.None) return true;

        if ((provider == AiProvider.OpenAI || provider == AiProvider.Claude) &&
            string.IsNullOrWhiteSpace(_txtApiKey.Password))
        {
            MessageBox.Show("API Key is required for this provider.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            _txtApiKey.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(_cmbModel.Text))
        {
            MessageBox.Show("Model name is required.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            _cmbModel.Focus();
            return false;
        }

        return true;
    }

    private async void BtnTest_Click(object sender, RoutedEventArgs e)
    {
        var provider = (AiProvider)_cmbProvider.SelectedIndex;
        _txtStatus.Text = "⏳ Testing...";
        _btnTest.IsEnabled = false;

        try
        {
            if (provider == AiProvider.LocalOllama)
            {
                var endpoint = string.IsNullOrWhiteSpace(_txtEndpoint.Text)
                    ? "http://localhost:11434" : _txtEndpoint.Text.Trim().TrimEnd('/');

                var resp = await _http.GetAsync($"{endpoint}/api/tags");
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    _txtStatus.Text = "✓ Ollama connected!";
                    _txtStatus.Foreground = Brushes.Green;
                }
                else
                {
                    _txtStatus.Text = "✗ Ollama not responding";
                    _txtStatus.Foreground = Brushes.Red;
                }
            }
            else if (provider == AiProvider.OpenAI)
            {
                var endpoint = string.IsNullOrWhiteSpace(_txtEndpoint.Text)
                    ? "https://api.openai.com/v1/models" : _txtEndpoint.Text.Trim().TrimEnd('/') + "/models";
                var key = _txtApiKey.Password.Trim();

                if (string.IsNullOrWhiteSpace(key))
                {
                    _txtStatus.Text = "✗ Enter API Key first";
                    _txtStatus.Foreground = Brushes.Red;
                    return;
                }

                var req = new HttpRequestMessage(HttpMethod.Get, endpoint);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
                var resp = await _http.SendAsync(req);

                _txtStatus.Text = resp.IsSuccessStatusCode ? "✓ OpenAI connected!" : "✗ Invalid API Key";
                _txtStatus.Foreground = resp.IsSuccessStatusCode ? Brushes.Green : Brushes.Red;
            }
            else if (provider == AiProvider.Claude)
            {
                var endpoint = string.IsNullOrWhiteSpace(_txtEndpoint.Text)
                    ? "https://api.anthropic.com/v1/messages" : _txtEndpoint.Text.Trim();
                var key = _txtApiKey.Password.Trim();

                if (string.IsNullOrWhiteSpace(key))
                {
                    _txtStatus.Text = "✗ Enter API Key first";
                    _txtStatus.Foreground = Brushes.Red;
                    return;
                }

                var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                req.Headers.Add("x-api-key", key);
                req.Headers.Add("anthropic-version", "2023-06-01");
                req.Content = new StringContent(
                    JsonSerializer.Serialize(new { model = "claude-3-haiku-20240307", max_tokens = 10, messages = new[] { new { role = "user", content = "hi" } } }),
                    System.Text.Encoding.UTF8, "application/json");

                var resp = await _http.SendAsync(req);
                _txtStatus.Text = resp.IsSuccessStatusCode ? "✓ Claude connected!" : "✗ Invalid API Key";
                _txtStatus.Foreground = resp.IsSuccessStatusCode ? Brushes.Green : Brushes.Red;
            }
        }
        catch (Exception ex)
        {
            _txtStatus.Text = $"✗ {ex.Message}";
            _txtStatus.Foreground = Brushes.Red;
        }

        _btnTest.IsEnabled = true;
    }

    private async void AutoDetectOllama()
    {
        var provider = (AiProvider)_cmbProvider.SelectedIndex;
        if (provider != AiProvider.LocalOllama || !(_chkEnabled.IsChecked ?? false)) return;

        try
        {
            var endpoint = string.IsNullOrWhiteSpace(_txtEndpoint.Text)
                ? "http://localhost:11434" : _txtEndpoint.Text.Trim().TrimEnd('/');

            var resp = await _http.GetAsync($"{endpoint}/api/tags");
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var models = doc.RootElement.GetProperty("models").EnumerateArray()
                    .Select(m => m.GetProperty("name").GetString() ?? "")
                    .Where(m => !string.IsNullOrEmpty(m))
                    .ToList();

                _cmbModel.Items.Clear();
                foreach (var m in models)
                    _cmbModel.Items.Add(m);

                if (models.Count > 0 && string.IsNullOrWhiteSpace(_cmbModel.Text))
                    _cmbModel.Text = models[0];

                _txtStatus.Text = $"✓ Ollama: {models.Count} model(s) found";
                _txtStatus.Foreground = Brushes.Green;
            }
        }
        catch (HttpRequestException)
        {
            _cmbModel.Items.Clear();
            _cmbModel.Items.Add("llama3.2");
            _cmbModel.Items.Add("llama3.1");
            _cmbModel.Items.Add("mistral");
            _cmbModel.Items.Add("codellama");
            _cmbModel.Items.Add("gemma2");
            if (string.IsNullOrWhiteSpace(_cmbModel.Text))
                _cmbModel.Text = "llama3.2";
            _txtStatus.Text = "⚠️ Ollama not found — enter model manually";
            _txtStatus.Foreground = Brushes.Orange;
        }
        catch (JsonException) { }
        catch (TaskCanceledException) { }
    }

    private void AddLabel(Panel panel, int row, int col, string text)
    {
        var tb = new TextBlock { Text = text, Margin = new Thickness(5), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(tb, row); Grid.SetColumn(tb, col);
        panel.Children.Add(tb);
    }
}
