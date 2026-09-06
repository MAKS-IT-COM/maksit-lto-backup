using System.ComponentModel;
using Avalonia.Controls;
using MaksIT.LTO.Backup.UI.ViewModels;


namespace MaksIT.LTO.Backup.UI;

public partial class MainWindow : Window {
  public MainWindow() {
    InitializeComponent();
  }

  public MainWindow(MainViewModel viewModel, IDialogService dialogs) : this() {
    dialogs.Owner = this;
    DataContext = viewModel;
    Closed += (_, _) => viewModel.Dispose();
    viewModel.PropertyChanged += OnViewModelPropertyChanged;
  }

  private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e) {
    if (e.PropertyName != nameof(MainViewModel.LogText))
      return;

    LogTextBox.CaretIndex = LogTextBox.Text?.Length ?? 0;
  }
}
