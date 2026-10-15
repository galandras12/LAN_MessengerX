using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using LanMessengerX.App.Localization;
using LanMessengerX.App.Services;
using LanMessengerX.App.ViewModels;
using LanMessengerX.Core;

namespace LanMessengerX.App.Views;

public partial class SettingsWindow : Window
{
    private AppState State => (AppState)DataContext!;
    private readonly List<ScrollViewer> _pages;
    private readonly List<string> _broadcasts = new();

    private sealed record Choice(string? Code, string Label)
    {
        public override string ToString() => Label;
    }

    public SettingsWindow()
    {
        InitializeComponent();
        _pages = new() { PageGeneral, PageAccount, PageMessages, PageHistory, PageAlerts, PageNetwork, PageFiles, PageLook, PageKeys };
        Nav.SelectedIndex = 0;
        FlowDirection = Loc.IsRightToLeft ? Avalonia.Media.FlowDirection.RightToLeft : Avalonia.Media.FlowDirection.LeftToRight;
        DataContextChanged += (_, _) => { if (DataContext is AppState s) Load(s); };
    }

    private void OnNavChanged(object? sender, SelectionChangedEventArgs e)
    {
        for (int i = 0; i < _pages.Count; i++) _pages[i].IsVisible = i == Nav.SelectedIndex;
        if (Nav.SelectedIndex == 5) { LogBox.Text = FileLog.Tail(); LogBox.CaretIndex = LogBox.Text?.Length ?? 0; }
    }

    // ---- settings -> controls ----------------------------------------------------------------------

    private void Load(AppState s)
    {
        var c = s.Settings;
        // general
        AutoStartBox.IsChecked = c.AutoStart;
        ShowMainBox.IsChecked = !c.StartMinimized;
        TrayBox.IsChecked = c.TrayIcon;
        CloseToTrayBox.IsChecked = c.CloseToTray;
        MinimizeTrayBox.IsChecked = c.MinimizeToTray;
        SingleClickBox.IsChecked = c.SingleClickTray;
        BalloonBox.IsChecked = c.Notifications;
        var langs = new List<Choice> { new("system", Loc.T("System default")) };
        langs.AddRange(Loc.Languages.Select(l => new Choice(l.Code, l.Name)));
        LanguageBox.ItemsSource = langs;
        LanguageBox.SelectedItem = langs.FirstOrDefault(l => l.Code == c.Language) ?? langs[0];
        // account
        NameBox.Text = s.Name;
        NoteBox.Text = s.Note;
        FirstNameBox.Text = c.FirstName;
        LastNameBox.Text = c.LastName;
        AboutBox.Text = c.About;
        RefreshBox.Value = c.RefreshSeconds;
        // messages
        EmoticonBox.IsChecked = c.ShowEmoticons;
        TimeBox.IsChecked = c.ShowTimestamp;
        DateBox.IsChecked = c.ShowDate;
        LinksBox.IsChecked = c.AllowLinks;
        PathLinkBox.IsChecked = c.PathToLink;
        TrimBox.IsChecked = c.TrimMessages;
        ClearBox.IsChecked = c.ClearOnClose;
        var fonts = Avalonia.Media.FontManager.Current.SystemFonts.Select(f => f.Name).Distinct().OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();
        if (!fonts.Contains(c.MessageFont)) fonts.Insert(0, c.MessageFont);
        FontBox.ItemsSource = fonts;
        FontBox.SelectedItem = c.MessageFont;
        FontSizeBox.Value = c.MessageFontSize;
        ColorBox.Text = c.MessageColor;
        TextSizeBox.Value = c.TextSize;
        // history
        HistoryBox.IsChecked = c.SaveHistory;
        HistoryDefaultRadio.IsChecked = string.IsNullOrWhiteSpace(c.HistoryDir);
        HistoryCustomRadio.IsChecked = !HistoryDefaultRadio.IsChecked;
        HistoryDirBox.Text = c.HistoryDir ?? "";
        FileHistoryBox.IsChecked = c.SaveFileHistory;
        // alerts
        AlertBox.IsChecked = c.StatusAlerts;
        NoBusyAlertBox.IsChecked = c.NoBusyAlert;
        NoDndAlertBox.IsChecked = c.NoDndAlert;
        SoundBox.IsChecked = c.Sounds;
        SndMsgBox.IsChecked = c.SoundNewMessage;
        SndOnlineBox.IsChecked = c.SoundUserOnline;
        SndOfflineBox.IsChecked = c.SoundUserOffline;
        SndFileBox.IsChecked = c.SoundNewFile;
        SndDoneBox.IsChecked = c.SoundFileDone;
        NoBusySoundBox.IsChecked = c.NoBusySound;
        NoDndSoundBox.IsChecked = c.NoDndSound;
        // network
        NetText.Text = s.Connected
            ? Loc.F("Connected as {0}", s.Ip ?? "") + "\n" + Loc.F("Your id on the network: {0}", s.LocalId)
            : Loc.T("Not connected. Join a network to find other people.");
        TimeoutBox.Value = c.TimeoutSeconds;
        RetriesBox.Value = c.MaxRetries;
        var adapters = new List<Choice> { new(null, Loc.T("Automatic")) };
        adapters.AddRange(NetUtil.ListInterfaces().Select(i => new Choice(i.Name, $"{i.Name} - {i.Ip}")));
        AdapterBox.ItemsSource = adapters;
        AdapterBox.SelectedItem = adapters.FirstOrDefault(a => a.Code == c.PreferredInterface) ?? adapters[0];
        ScanBox.IsChecked = c.ScanSubnet;
        _broadcasts.Clear();
        _broadcasts.AddRange(c.ExtraBroadcasts);
        BroadcastList.ItemsSource = null;
        BroadcastList.ItemsSource = _broadcasts.ToList();
        MulticastBox.Text = c.Multicast;
        UdpBox.Value = c.UdpPort;
        TcpBox.Value = c.TcpPort;
        FirewallBtn.IsVisible = Firewall.Supported;
        // file transfer
        AutoFileBox.IsChecked = c.AutoAcceptFiles;
        ShowTransfersBox.IsChecked = c.ShowTransferWindow;
        DownloadBox.Text = c.EffectiveDownloadDir;
        // look & keys
        ThemeBox.SelectedIndex = c.Theme switch { "light" => 1, "dark" => 2, _ => 0 };
        TooltipBox.IsChecked = c.ContactTooltips;
        EnterRadio.IsChecked = c.SendWithEnter;
        CtrlEnterRadio.IsChecked = !c.SendWithEnter;
    }

    // ---- controls -> settings ----------------------------------------------------------------------

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        var s = State;
        var c = s.Settings;
        c.AutoStart = AutoStartBox.IsChecked == true;
        c.StartMinimized = ShowMainBox.IsChecked != true;
        c.TrayIcon = TrayBox.IsChecked == true;
        c.CloseToTray = CloseToTrayBox.IsChecked == true;
        c.MinimizeToTray = MinimizeTrayBox.IsChecked == true;
        c.SingleClickTray = SingleClickBox.IsChecked == true;
        c.Notifications = BalloonBox.IsChecked == true;
        c.Language = (LanguageBox.SelectedItem as Choice)?.Code ?? "system";

        s.SetProfile(NameBox.Text ?? "", NoteBox.Text ?? "");
        c.FirstName = (FirstNameBox.Text ?? "").Trim();
        c.LastName = (LastNameBox.Text ?? "").Trim();
        c.About = (AboutBox.Text ?? "").Trim();
        c.RefreshSeconds = (int)(RefreshBox.Value ?? 60);

        c.ShowEmoticons = EmoticonBox.IsChecked == true;
        c.ShowTimestamp = TimeBox.IsChecked == true;
        c.ShowDate = DateBox.IsChecked == true;
        c.AllowLinks = LinksBox.IsChecked == true;
        c.PathToLink = PathLinkBox.IsChecked == true;
        c.TrimMessages = TrimBox.IsChecked == true;
        c.ClearOnClose = ClearBox.IsChecked == true;
        c.MessageFont = FontBox.SelectedItem as string ?? c.MessageFont;
        c.MessageFontSize = (int)(FontSizeBox.Value ?? 9);
        var color = (ColorBox.Text ?? "").Trim();
        if (Color.TryParse(color, out _) && color.StartsWith('#')) c.MessageColor = color;
        c.TextSize = (int)(TextSizeBox.Value ?? 11);

        c.SaveHistory = HistoryBox.IsChecked == true;
        var newHistoryDir = HistoryCustomRadio.IsChecked == true && !string.IsNullOrWhiteSpace(HistoryDirBox.Text) ? HistoryDirBox.Text!.Trim() : null;
        if (newHistoryDir != c.HistoryDir)
        {
            c.HistoryDir = newHistoryDir;
            s.UseHistoryFolder();
        }
        c.SaveFileHistory = FileHistoryBox.IsChecked == true;

        c.StatusAlerts = AlertBox.IsChecked == true;
        c.NoBusyAlert = NoBusyAlertBox.IsChecked == true;
        c.NoDndAlert = NoDndAlertBox.IsChecked == true;
        c.Sounds = SoundBox.IsChecked == true;
        c.SoundNewMessage = SndMsgBox.IsChecked == true;
        c.SoundUserOnline = SndOnlineBox.IsChecked == true;
        c.SoundUserOffline = SndOfflineBox.IsChecked == true;
        c.SoundNewFile = SndFileBox.IsChecked == true;
        c.SoundFileDone = SndDoneBox.IsChecked == true;
        c.NoBusySound = NoBusySoundBox.IsChecked == true;
        c.NoDndSound = NoDndSoundBox.IsChecked == true;

        c.TimeoutSeconds = (int)(TimeoutBox.Value ?? 10);
        c.MaxRetries = (int)(RetriesBox.Value ?? 1);
        c.PreferredInterface = (AdapterBox.SelectedItem as Choice)?.Code;
        c.ScanSubnet = ScanBox.IsChecked == true;
        c.ExtraBroadcasts = _broadcasts.ToList();
        var mc = (MulticastBox.Text ?? "").Trim();
        c.Multicast = System.Net.IPAddress.TryParse(mc, out _) ? mc : Proto.DefaultMulticast;
        c.UdpPort = (int)(UdpBox.Value ?? Proto.DefaultPort);
        c.TcpPort = (int)(TcpBox.Value ?? Proto.DefaultPort);

        c.AutoAcceptFiles = AutoFileBox.IsChecked == true;
        c.ShowTransferWindow = ShowTransfersBox.IsChecked == true;
        var dl = DownloadBox.Text?.Trim();
        c.DownloadDir = string.IsNullOrEmpty(dl) || dl == new AppSettings().EffectiveDownloadDir ? null : dl;

        c.Theme = ThemeBox.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "system" };
        c.ContactTooltips = TooltipBox.IsChecked == true;
        c.SendWithEnter = EnterRadio.IsChecked == true;

        s.ApplySettings();
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private async void OnReset(object? sender, RoutedEventArgs e)
    {
        if (!await Dialogs.Confirm(this, Loc.T("Reset Preferences"), Loc.T("All preferences go back to their defaults. Your name, picture and groups stay."), Loc.T("Reset Preferences"))) return;
        State.ResetPreferences();
        Load(State);
    }

    // ---- buttons ------------------------------------------------------------------------------------

    private async void OnChoosePicture(object? sender, RoutedEventArgs e)
    {
        var files = await Pickers.OpenFiles(this, Loc.T("Select avatar picture"), multiple: false, imagesOnly: true);
        if (files.Count == 0) return;
        var error = Avatars.UseFile(State, files[0]);
        if (error != null) await Dialogs.Message(this, Loc.T("Select avatar picture"), Loc.F("This picture could not be used: {0}", error));
    }

    private void OnRemovePicture(object? sender, RoutedEventArgs e)
    {
        State.Settings.StockAvatar = -1;
        State.SetAvatar(null);
    }

    private async void OnBrowse(object? sender, RoutedEventArgs e)
    {
        var dir = await Pickers.OpenFolder(this, Loc.T("Store Received Files in this Folder"));
        if (dir != null) DownloadBox.Text = dir;
    }

    private async void OnBrowseHistory(object? sender, RoutedEventArgs e)
    {
        var dir = await Pickers.OpenFolder(this, Loc.T("History File Location"));
        if (dir == null) return;
        HistoryDirBox.Text = dir;
        HistoryCustomRadio.IsChecked = true;
    }

    private void OnViewFiles(object? sender, RoutedEventArgs e)
    {
        var dir = string.IsNullOrWhiteSpace(DownloadBox.Text) ? State.Settings.EffectiveDownloadDir : DownloadBox.Text!;
        Directory.CreateDirectory(dir);
        Shell.Open(dir);
    }

    private async void OnClearHistory(object? sender, RoutedEventArgs e)
    {
        if (await Dialogs.Confirm(this, Loc.T("Clear Message History"), Loc.T("Delete all saved conversations?"), Loc.T("Clear Message History"))) State.History.Clear();
    }

    private async void OnClearFileHistory(object? sender, RoutedEventArgs e)
    {
        if (await Dialogs.Confirm(this, Loc.T("Clear Transfer History"), Loc.T("Delete the list of earlier file transfers?"), Loc.T("Clear Transfer History"))) TransferLog.Clear();
    }

    private void OnAddBroadcast(object? sender, RoutedEventArgs e)
    {
        var t = (BroadcastBox.Text ?? "").Trim();
        if (!System.Net.IPAddress.TryParse(t, out _) || _broadcasts.Contains(t)) return;
        _broadcasts.Add(t);
        BroadcastList.ItemsSource = _broadcasts.ToList();
        BroadcastBox.Text = "";
    }

    private void OnDeleteBroadcast(object? sender, RoutedEventArgs e)
    {
        if (BroadcastList.SelectedItem is not string t) return;
        _broadcasts.Remove(t);
        BroadcastList.ItemsSource = _broadcasts.ToList();
    }

    private void OnFirewall(object? sender, RoutedEventArgs e)
    {
        Firewall.Allow(out var msg);
        FirewallText.Text = Loc.T(msg);
        State.Messenger?.Refresh();
        LogBox.Text = FileLog.Tail();
    }

    private void OnRefreshNetwork(object? sender, RoutedEventArgs e)
    {
        State.Messenger?.Refresh();
        LogBox.Text = FileLog.Tail();
        LogBox.CaretIndex = LogBox.Text?.Length ?? 0;
    }

    private async void OnCopyLog(object? sender, RoutedEventArgs e)
    {
        try
        {
            string text;
            try { text = File.ReadAllText(FileLog.Path_); } catch (Exception) { text = FileLog.Tail(200); }
            if (text.Length > 60000) text = text[^60000..];
            if (Clipboard is { } cb) await cb.SetTextAsync(text);
            FirewallText.Text = Loc.T("The log was copied to the clipboard.");
        }
        catch (Exception ex) { FirewallText.Text = ex.Message; }
    }

    private void OnOpenLog(object? sender, RoutedEventArgs e) => Shell.Reveal(FileLog.Path_);
}
