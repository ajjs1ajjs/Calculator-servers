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

    public Task<bool> ConfirmAsync(string message, string title)
        => Task.FromResult(MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes);

    public Task InfoAsync(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    public Task ErrorAsync(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        return Task.CompletedTask;
    }

    public Task<bool> ShowPasswordDialogAsync()
    {
        var dialog = new PasswordDialog(_access, _owner());
        var result = dialog.ShowDialog() == true && dialog.Unlocked;
        return Task.FromResult(result);
    }

    public Task ShowChangePasswordDialogAsync()
    {
        new ChangePasswordDialog(_access, _owner()).ShowDialog();
        return Task.CompletedTask;
    }

    public Task<string?> PickSavePathAsync(string defaultFileName, string filterDescription, string extension)
    {
        var dialog = new SaveFileDialog
        {
            Filter = $"{filterDescription}|*{extension}",
            FileName = defaultFileName
        };
        return Task.FromResult<string?>(dialog.ShowDialog() == true ? dialog.FileName : null);
    }
}