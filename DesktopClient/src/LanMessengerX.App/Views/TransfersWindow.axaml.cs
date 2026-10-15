using Avalonia.Controls;
using Avalonia.Interactivity;
using LanMessengerX.App.Localization;
using LanMessengerX.App.Services;
using LanMessengerX.App.ViewModels;
using LanMessengerX.Core;

namespace LanMessengerX.App.Views;

public partial class TransfersWindow : Window
{
    private AppState State => (AppState)DataContext!;

    public TransfersWindow()
    {
        InitializeComponent();
        FlowDirection = Loc.IsRightToLeft ? Avalonia.Media.FlowDirection.RightToLeft : Avalonia.Media.FlowDirection.LeftToRight;
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not AppState s) return;
            s.Transfers.CollectionChanged += (_, _) => Refresh();
            Refresh();
        };
    }

    private void Refresh()
    {
        var history = State.Settings.SaveFileHistory ? TransferLog.Read().Take(60).ToList() : new List<TransferLog.Entry>();
        HistoryList.Items.Clear();
        foreach (var e in history)
        {
            HistoryList.Items.Add(new StackPanel
            {
                Margin = new Avalonia.Thickness(0, 4),
                Children =
                {
                    new TextBlock { Text = e.Name, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis },
                    new TextBlock
                    {
                        Text = $"{Loc.F(e.Outgoing ? "To {0}" : "From {0}", e.Peer)} · {FileVm.Format(e.Size)} · {Loc.T(StateName(e.State))} · {DateTimeOffset.FromUnixTimeMilliseconds(e.Time).ToLocalTime():g}",
                        FontSize = 12, Opacity = 0.65, TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    },
                },
            });
        }
        HistoryTitle.IsVisible = history.Count > 0;
        EmptyText.IsVisible = State.Transfers.Count == 0 && history.Count == 0;
    }

    private static string StateName(string s) => s switch
    {
        nameof(TransferState.Complete) => "Completed",
        nameof(TransferState.Declined) => "Declined",
        nameof(TransferState.Cancelled) => "Cancelled",
        _ => "Transfer failed",
    };

    private void OnViewFiles(object? sender, RoutedEventArgs e)
    {
        var dir = State.Settings.EffectiveDownloadDir;
        Directory.CreateDirectory(dir);
        Shell.Open(dir);
    }

    private void OnClearFinished(object? sender, RoutedEventArgs e)
    {
        foreach (var f in State.Transfers.Where(f => f.State is TransferState.Complete or TransferState.Declined or TransferState.Cancelled or TransferState.Error).ToList())
            State.Transfers.Remove(f);
    }

    private void OnClearHistory(object? sender, RoutedEventArgs e)
    {
        TransferLog.Clear();
        Refresh();
    }
}
