using Avalonia.Controls;


namespace MaksIT.LTO.Backup.UI;

public interface IDialogService {
  Window? Owner { get; set; }

  Task<bool> ConfirmAsync(string title, string message);
}

public sealed class DialogService : IDialogService {
  public Window? Owner { get; set; }

  public async Task<bool> ConfirmAsync(string title, string message) {
    if (Owner is null)
      return false;

    return await ConfirmDialog.ShowAsync(Owner, title, message);
  }
}
