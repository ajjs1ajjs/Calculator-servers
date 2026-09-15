using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ResourceCalculator.Localization;
using ResourceCalculator.Services;

namespace ResourceCalculator.Views;

public partial class PasswordDialog : Window
{
    // null лише в безпараметрному конструкторі для дизайнера; у рантаймі завжди задано.
    private readonly AccessService? _access;
    private readonly bool _setupMode;

    public PasswordDialog()
    {
        InitializeComponent();
    }

    public PasswordDialog(AccessService access, Window? owner)
    {
        _access = access;
        // Fail closed: за відсутності settings.json вбудованого пароля більше немає —
        // перший запуск вимагає СТВОРИТИ пароль, а не вгадати дефолт.
        _setupMode = !access.IsPasswordSet;
        InitializeComponent();
        DataContext = this;
        var loc = LocalizationService.Instance;
        if (_setupMode)
        {
            UnlockPanel.IsVisible = false;
            SetupPanel.IsVisible = true;
            ForgotPanel.IsVisible = false;
            TxtTitle.Text = loc["access.setupTitle"];
            TxtSubtitle.Text = loc["access.setupSubtitle"];
            BtnOk.Content = loc["access.create"];
            TxtNewPassword.Focus();
        }
        else
        {
            TxtPassword.Focus();
        }
    }

    public string PasswordHint => _access?.GetPasswordHint() ?? "";

    public bool Unlocked { get; private set; }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (_setupMode) TryCreate();
        else TryUnlock();
    }

    private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (_setupMode) TryCreate();
            else TryUnlock();
        }
    }

    private void TryUnlock()
    {
        if (_access is null) return;
        var remaining = _access.LockoutRemaining;
        if (remaining > TimeSpan.Zero)
        {
            ShowError(string.Format(LocalizationService.Instance["access.lockedOut"], (int)remaining.TotalSeconds));
            return;
        }
        // Платформне обмеження: в Avalonia 12 немає штатного PasswordBox —
        // лишається TextBox + PasswordChar. Компенсації: очищення контрола одразу
        // після перевірки та тротлінг спроб на рівні AccessService.
        var pwd = TxtPassword.Text ?? "";
        var ok = _access.Verify(pwd);
        TxtPassword.Clear();
        if (ok)
        {
            Unlocked = true;
            Close(Unlocked);
        }
        else
        {
            var wait = _access.LockoutRemaining;
            ShowError(wait > TimeSpan.Zero
                ? string.Format(LocalizationService.Instance["access.lockedOut"], (int)wait.TotalSeconds)
                : LocalizationService.Instance["access.error"]);
            TxtPassword.Focus();
        }
    }

    private void TryCreate()
    {
        if (_access is null) return;
        var loc = LocalizationService.Instance;
        var p1 = TxtNewPassword.Text ?? "";
        var p2 = TxtConfirmPassword.Text ?? "";
        TxtNewPassword.Clear();
        TxtConfirmPassword.Clear();
        if (p1.Length < AccessService.MinPasswordLength)
        {
            ShowError(loc["access.errorTooShort"]);
            TxtNewPassword.Focus();
            return;
        }
        if (!string.Equals(p1, p2, StringComparison.Ordinal))
        {
            ShowError(loc["access.errorMismatch"]);
            TxtNewPassword.Focus();
            return;
        }
        try
        {
            _access.SetPassword(p1);
            Unlocked = true;
            Close(Unlocked);
        }
        catch (Exception ex)
        {
            ShowError($"{loc["access.changeError"]} {ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        TxtError.Text = message;
        TxtError.IsVisible = true;
    }
}
