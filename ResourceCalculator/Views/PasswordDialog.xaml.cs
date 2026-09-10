using System.Windows;
using System.Windows.Input;
using ResourceCalculator.Localization;
using ResourceCalculator.Services;

namespace ResourceCalculator.Views;

public partial class PasswordDialog : Window
{
    private readonly AccessService _access;

    public PasswordDialog(AccessService access, Window? owner)
    {
        _access = access;
        InitializeComponent();
        Owner = owner;
        DataContext = this;
        TxtPassword.Focus();
        Loaded += (_, _) => TxtPassword.Focus();
    }

    public string PasswordHint => _access.GetPasswordHint();

    public bool Unlocked { get; private set; }

    private void BtnOk_Click(object sender, RoutedEventArgs e) => TryUnlock();

    private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) TryUnlock();
    }

    private void TryUnlock()
    {
        if (_access.Verify(TxtPassword.Password))
        {
            Unlocked = true;
            DialogResult = true;
            Close();
        }
        else
        {
            TxtError.Visibility = Visibility.Visible;
            TxtPassword.Clear();
            TxtPassword.Focus();
        }
    }
}
