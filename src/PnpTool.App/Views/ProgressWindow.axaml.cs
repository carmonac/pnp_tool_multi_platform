using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Views;

/// <summary>
/// Shows the progress of a long task that runs on another thread, with the time it is
/// expected to take and a button to stop it. It closes by itself when the task ends.
/// Original: <c>Window_BarraProgreso</c>.
/// </summary>
public partial class ProgressWindow : Window
{
    private readonly Func<Action<int>, CancellationToken, Task> _work;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly DateTime _started = DateTime.Now;
    private bool _finished;

    public ProgressWindow()
        : this(string.Empty, (_, _) => Task.CompletedTask)
    {
    }

    /// <param name="caption">What is being done.</param>
    /// <param name="work">The task. It is given a way to report its progress, from 0 to 100, and a signal that asks it to stop.</param>
    public ProgressWindow(string caption, Func<Action<int>, CancellationToken, Task> work)
    {
        InitializeComponent();
        _work = work;
        Title = caption;
        CaptionLabel.Text = caption;
        ToolTip.SetTip(CancelButton, StringResources.Get("Form_Setup_Boton_Cancelar"));
        Opened += async (_, _) => await RunAsync();
        Closing += (_, e) =>
        {
            // Closing the window asks the task to stop; the window goes when the task has.
            if (!_finished)
            {
                _cancellation.Cancel();
                e.Cancel = true;
            }
        };
    }

    /// <summary>What went wrong in the task, if anything.</summary>
    public Exception? Failure { get; private set; }

    /// <summary>Whether the user asked the task to stop.</summary>
    public bool WasCancelled => _cancellation.IsCancellationRequested;

    /// <summary>Asks the task to stop, as the cancel button does.</summary>
    public void Cancel() => _cancellation.Cancel();

    /// <summary>
    /// Shows a percentage and the seconds left at the pace so far.
    /// Original: <c>Trabajador_Actualiza</c>.
    /// </summary>
    public void Report(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        Bar.Value = percent;
        PercentLabel.Text = percent + "%";
        if (percent > 0)
        {
            double left = (100 - percent) * (DateTime.Now - _started).TotalSeconds / percent;
            EtaLabel.Text = "ETA: " + Math.Round(left) + " s";
        }
    }

    private async Task RunAsync()
    {
        try
        {
            await Task.Run(() => _work(percent => Dispatcher.UIThread.Post(() => Report(percent)), _cancellation.Token));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Failure = e;
        }

        _finished = true;
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Cancel();
}
