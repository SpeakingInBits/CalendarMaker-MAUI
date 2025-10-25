using CalendarMaker_MAUI.Services;

namespace CalendarMaker_MAUI.Views;

public partial class ExportProgressModal : ContentPage
{
    private CancellationTokenSource? _cancellationTokenSource;

    public event EventHandler? Cancelled;

    public ExportProgressModal()
    {
        InitializeComponent();
        CancelBtn.Clicked += OnCancelClicked;
    }

    public void UpdateProgress(ExportProgress progress)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusLabel.Text = progress.Status;
            ProgressLabel.Text = $"{progress.CurrentPage} / {progress.TotalPages}";
            ExportProgressBar.Progress = progress.TotalPages > 0 ? (double)progress.CurrentPage / progress.TotalPages : 0;
            CancelBtn.IsEnabled = progress.CanCancel;
        });
    }

    public void SetCancellationTokenSource(CancellationTokenSource cts)
    {
        _cancellationTokenSource = cts;
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        Cancelled?.Invoke(this, EventArgs.Empty);
    }
}