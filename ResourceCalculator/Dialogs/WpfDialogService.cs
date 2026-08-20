using System.Windows;
using Microsoft.Win32;
using ResourceCalculator.Interfaces;
using ResourceCalculator.Services;
using ResourceCalculator.Views;

namespace ResourceCalculator.Dialogs;

// WPF-реалізація діалогів і вибору файлу (Windows). Avalonia має свою.
public class WpfDialogService : IDialogService, IFileSaveService
{
    private readonly AccessService _access;
    private readonly Func<Window?> _owner;

    public WpfDialogService(AccessService access, Func<Window?> owner)
    {
        _access = access;
        _owner = owner;
    }

    public bool Confirm(string message, string title)
        => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public void Info(string message, string title)
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void Error(string message, string title)
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool ShowPasswordDialog()
    {
        var dialog = new PasswordDialog(_access, _owner());
        return dialog.ShowDialog() == true && dialog.Unlocked;
    }

    public void ShowChangePasswordDialog()
        => new ChangePasswordDialog(_access, _owner()).ShowDialog();

    public string? PickSavePath(string defaultFileName, string filterDescription, string extension)
    {
        var dialog = new SaveFileDialog
        {
            Filter = $"{filterDescription}|*{extension}",
            FileName = defaultFileName
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}