using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;
using ExileLens.Core;
using Forms = System.Windows.Forms;

namespace ExileLens;
public partial class MainWindow : Window
{
    private readonly Settings settings;
    private readonly bool smoke;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(750) };
    private LogTail tail;
    private Forms.NotifyIcon? tray;
    private IntPtr handle;
    private IntPtr lastGameWindow;
    private HwndSource? source;
    private bool locked, exiting, dismissed, ready;
    private bool lockHotkeyAvailable;
    private AreaEntry? currentArea;
    private WatchEntry? selected;
    private RuneHelperWindow? runeHelper;
    private void OpenRuneHelper(object? sender = null, RoutedEventArgs? e = null) { runeHelper ??= new RuneHelperWindow(economy, smoke) { Resources = Resources }; runeHelper.Open(settings.League); Hide(); }
    private readonly ExpeditionWindow expedition;
    private readonly EvaluationWindow evaluation;
    private readonly CancellationTokenSource lifetime = new();
    private bool checking;
    private CopiedItem? currentItem;
    private CheckHotkey activeShortcut = CheckHotkey.Default;
    private int checkHotkeyId = 1;
    private bool checkRegistered;
    private readonly LeagueService leagueService = new();
    private System.Collections.Generic.IReadOnlyList<LeagueOption> leagueOptions = Leagues.Bundled;

    public MainWindow(bool smoke = false)
    {
        this.smoke = smoke;
        string? loadError = null;
        settings = smoke ? new Settings() : SettingsStore.Load(out loadError);
        tail = new LogTail(settings.LogPath);
        InitializeComponent();
        InitializeMarketBrowser();
        LoadCharacter();
        Tabs.SelectedItem = HomeTab;
        expedition = new ExpeditionWindow { Resources = Resources };
        expedition.Dismissed += () => dismissed = true;
        evaluation = new EvaluationWindow(smoke) { Resources = Resources, IsTestMode = smoke };
        evaluation.BookmarkOpened += saved => { ShowItem(saved); evaluation.Show(); evaluation.Activate(); _ = LookupEstimateAsync(); };
        evaluation.HistoryRequested += NavigateRecent;
        evaluation.PinRequested += () => PinComparison(this, new RoutedEventArgs());
        evaluation.ComparePinnedRequested += () =>
        {
            if (currentItem == null) return;
            if (comparisonBaseline == null) { PinComparison(this, new RoutedEventArgs()); evaluation.SetQuote("Comparison pinned. Inspect another item, then choose Pin / compare items."); return; }
            new ListingComparisonWindow(comparisonBaseline, currentItem) { Resources = Resources }.Show();
        };
        evaluation.FiltersChanged += ResetQuote;
        evaluation.Dismissed += ResetQuote;
        evaluation.ImportRequested += ImportComparableListings;
        evaluation.RefreshRequested += () => _ = LookupEstimateAsync();
        evaluation.SettingsRequested += () => { ShowManual(); Tabs.SelectedItem = SettingsTab; };
        evaluation.SearchRequested += filters =>
        {
            if (currentItem == null) return;
            try
            {
                string url = TradeSearch.BuildUrl(currentItem, settings.League, filters);
                if (!smoke) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Win32Exception) { evaluation.SetQuote("Could not open the default browser."); }
        };
        Left = double.IsFinite(settings.Left) ? Math.Clamp(settings.Left, SystemParameters.VirtualScreenLeft, Math.Max(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width)) : 80;
        Top = double.IsFinite(settings.Top) ? Math.Clamp(settings.Top, SystemParameters.VirtualScreenTop, Math.Max(SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height)) : 80;
        expedition.Left = Math.Clamp(Left + Width + 12, SystemParameters.VirtualScreenLeft, Math.Max(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - expedition.Width));
        expedition.Top = Top;
        PopulateSettings();
        RefreshWatchlist();
        if (loadError != null) Notice.Text = loadError;
        RestoreSession();
        UpdateSession();
        WindowPlacement.Attach(this, "main"); WindowPlacement.Attach(expedition, "expedition"); WindowPlacement.Attach(evaluation, "evaluation");
        ready = true;
        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
        timer.Tick += (_, _) => { PollLog(); if(IsVisible && Tabs.SelectedItem == HomeTab) UpdateHome(); };
        if (!smoke) { PollLog(); timer.Start(); }
        if (!smoke) Loaded += async (_, _) => await LoadLeaguesAsync();
    }

    private void PopulateSettings()
    {
        LogPathBox.Text = settings.LogPath;
        SetLeagueChoices(leagueOptions, settings.League);
        MetadataLegend.ItemsSource = ItemMetadata.Styles;
        CurrencyBox.ItemsSource = new[] { "Exalted Orb", "Divine Orb", "Chaos Orb" }
            .Append(settings.Currency).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        CurrencyBox.SelectedItem = settings.Currency;
        evaluation.SetPreferredCurrency(settings.Currency);
        AutoCheck.IsChecked = settings.AutoShow;
        PinCheck.IsChecked = settings.Pinned;
        OpacitySlider.Value = settings.Opacity;
        ApplyOverlayOpacity(settings.Opacity);
        activeShortcut = CheckHotkey.Parse(settings.PriceCheckHotkey) ?? CheckHotkey.Default;
        HotkeyBox.Text = activeShortcut.Label;
        UpdateShortcutLabels();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        handle = new WindowInteropHelper(this).Handle;
        source = HwndSource.FromHwnd(handle);
        source.AddHook(WindowProc);
        var failures = new System.Collections.Generic.List<string>();
        if (!smoke)
        {
            checkRegistered = Native.RegisterHotKey(handle, checkHotkeyId, activeShortcut.Modifiers | 0x4000, activeShortcut.Key);
            if (!checkRegistered) failures.Add(activeShortcut.Label);
            if (!Native.RegisterHotKey(handle, 2, 0x4003, 0x4F)) failures.Add("Ctrl+Alt+O");
            lockHotkeyAvailable = Native.RegisterHotKey(handle, 3, 0x4003, 0x4C);
            if (!lockHotkeyAvailable) failures.Add("Ctrl+Alt+L");
            if (failures.Count > 0) Notice.Text = "Shortcut already in use: " + string.Join(", ", failures) + ". Use the tray menu.";
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Show overlay / unlock", null, (_, _) => Dispatcher.Invoke(() => ShowManual()));
            menu.Items.Add("Rune choice helper / pause scanning", null, (_, _) => Dispatcher.Invoke(() => OpenRuneHelper()));
            menu.Items.Add("Inspect clipboard", null, (_, _) => Dispatcher.Invoke(ReadClipboard));
            menu.Items.Add("Toggle click-through", null, (_, _) => Dispatcher.Invoke(ToggleLock));
            menu.Items.Add("Reset window placement", null, (_, _) => Dispatcher.Invoke(WindowPlacement.Reset));
            menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(ExitApplication));
            tray = new Forms.NotifyIcon { Text = "Exile Lens · POE2 overlay", Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!), ContextMenuStrip = menu, Visible = true };
            TrayMenuPlacement.Attach(menu);
            tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => ShowManual());
        }
        else lockHotkeyAvailable = true;
    }

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != 0x0312) return IntPtr.Zero;
        handled = true;
        if (wParam.ToInt32() == checkHotkeyId) { _ = CheckHoveredItemAsync(); return IntPtr.Zero; }
        switch (wParam.ToInt32())
        {
            case 2: if (IsVisible) HidePanel(); else ShowManual(); break;
            case 3: ToggleLock(); break;
        }
        return IntPtr.Zero;
    }

    private void PollLog()
    {
        UpdateSession();
        var foregroundGame = Native.ForegroundGameWindow();
        if (foregroundGame != IntPtr.Zero) lastGameWindow = foregroundGame;
        try
        {
            foreach (var area in tail.Poll()) ApplyArea(area);
            LogStatus.Text = currentArea == null ? "Log connected · waiting for an area change" : $"Level {currentArea.Level} · log connected";
            UpdateExpeditionVisibility();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            LogStatus.Text = "Cannot read game log · check the path in Settings";
        }
    }

    private void ApplyArea(AreaEntry area)
    {
        bool enteredNewInstance = currentArea?.Id != area.Id || currentArea?.Seed != area.Seed;
        if (enteredNewInstance) RecordArea(area);
        currentArea = area;
        AreaLabel.Text = area.DisplayName;
        if (enteredNewInstance) dismissed = false;
        expedition.Refresh(settings, area);
        if (enteredNewInstance && area.IsExpedition && !smoke) _ = LoadAutomaticGuideAsync(area, settings.League);
        UpdateExpeditionVisibility();
    }

    private void UpdateExpeditionVisibility()
    {
        bool context = currentArea?.IsExpedition == true || (settings.Pinned && expedition.IsVisible);
        bool show = settings.AutoShow && context && !dismissed && (smoke || Native.GameIsForeground() || expedition.IsActive || (settings.Pinned && expedition.IsVisible));
        if (show && !expedition.IsVisible) expedition.Show();
        else if (!show && expedition.IsVisible) expedition.Hide();
    }

    private void ShowManual()
    {
        SetLocked(false);
        ShowActivated = true;
        Show();
        Activate();
    }
    private void HidePanel() { Hide(); }
    private void HideClick(object sender, RoutedEventArgs e) => HidePanel();
    private void ExitClick(object sender, RoutedEventArgs e) => ExitApplication();
    public void ExitApplication() { exiting = true; Close(); }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!exiting) { e.Cancel = true; HidePanel(); return; }
        timer.Stop();
        runeHelper?.Shutdown();
        SaveSession();
        lifetime.Cancel();
        for (int id = 1; id <= 4; id++) Native.UnregisterHotKey(handle, id);
        expedition.Close();
        evaluation.Close();
        source?.RemoveHook(WindowProc);
        if (tray != null) { tray.Visible = false; var trayIcon = tray.Icon; var trayMenu = tray.ContextMenuStrip; tray.Dispose(); trayMenu?.Dispose(); trayIcon?.Dispose(); }
        // Position is saved only on explicit settings/watchlist writes, preserving unreadable settings on exit.
    }
    private void DragHeaderDelta(object sender, DragDeltaEventArgs e)
    {
        if (locked) return;
        // Thumb captures the mouse throughout the drag, including when the
        // overlay was shown without activation. Deltas use WPF's DPI-aware units.
        Left += e.HorizontalChange;
        Top += e.VerticalChange;
        e.Handled = true;
    }
    private void DragHeaderCompleted(object sender, DragCompletedEventArgs e) { if (!locked) Persist(); }
    private void ToggleLockClick(object sender, RoutedEventArgs e) => ToggleLock();
    private void ToggleLock()
    {
        if (!lockHotkeyAvailable && tray == null) { Notice.Text = "Click-through unavailable: no unlock shortcut or tray menu."; return; }
        SetLocked(!locked);
    }
    private void SetLocked(bool value)
    {
        locked = value;
        if (handle != IntPtr.Zero)
        {
            int style = Native.GetWindowLong(handle, -20);
            Native.SetWindowLong(handle, -20, value ? style | 0x20 | 0x08000000 : style & ~0x20 & ~0x08000000);
        }
        LockState.Text = value ? "Click-through · Ctrl+Alt+L to unlock" : "POE2 companion";
        Notice.Text = value ? "Mouse input passes through · unlock from shortcut or tray" : "Ctrl+Alt+O to show or hide";
    }

    private void ReadClipboardClick(object sender, RoutedEventArgs e) => ReadClipboard();
    private async Task CheckHoveredItemAsync()
    {
        if (checking || exiting) return;
        var focusedWindow = Native.GetForegroundWindow();
        if (Native.ForegroundGameWindow() == IntPtr.Zero && (focusedWindow == handle || focusedWindow == new WindowInteropHelper(evaluation).Handle) && Native.IsGameWindow(lastGameWindow))
        {
            evaluation.Hide(); Hide();
            Native.SetForegroundWindow(lastGameWindow);
        }
        if (Native.ForegroundGameWindow() == IntPtr.Zero) { Notice.Text = "Hover an item in POE2 before using the item-check shortcut."; CaptureDiagnostics.Record("game-focus", 0); return; }
        checking = true;
        SearchStatus.Text = "Copying hovered item · keep your cursor on the item…";
        SearchButton.IsEnabled = false;
        ResetQuote();
        currentItem = null;
        var elapsed = Stopwatch.StartNew();
        CaptureDiagnostics.Record("started", 0);
        try
        {
            var result = await new ItemCapture(new WindowsItemCapture(() => activeShortcut)).CaptureAsync(lifetime.Token);
            CaptureDiagnostics.Record(result.Stage, elapsed.ElapsedMilliseconds);
            if (exiting) return;
            // Preserve the game's keyboard focus for the next item check. Only
            // explicitly opening the panel from the tray/show shortcut activates it.
            SetLocked(false);
            Hide();
            evaluation.ShowActivated = false;
            evaluation.FitToScreen(!double.IsFinite(evaluation.Left));
            evaluation.Show();
            ShowItem(result.Item);
            if (result.Error != null) { evaluation.SetQuote(result.Error); SearchStatus.Text = result.Error; Notice.Text = $"Item check failed · {result.Stage}"; return; }
            await LookupEstimateAsync();
        }
        catch (OperationCanceledException) { CaptureDiagnostics.Record("cancelled", elapsed.ElapsedMilliseconds); }
        finally { checking = false; }
    }
    private void ReadClipboard()
    {
        if (checking) return;
        ShowItem(null);
        try
        {
            var item = System.Windows.Clipboard.ContainsText() ? ItemParser.Parse(System.Windows.Clipboard.GetText()) : null;
            ShowItem(item);
            if (item != null)
            {
                Hide();
                evaluation.FitToScreen(!double.IsFinite(evaluation.Left));
                evaluation.Show();
                evaluation.Activate();
                _ = LookupEstimateAsync();
            }
            else { ShowManual(); Tabs.SelectedItem = HomeTab; }
        }
        catch (ExternalException) { Notice.Text = "Clipboard is busy. Copy the item again and retry."; }
    }
    private void ShowItem(CopiedItem? item)
    {
        ResetQuote();
        currentItem = item;
        RecordSessionLoot.IsEnabled = item != null;
        SessionLootItem.Text = item == null ? "Price-check an item first." : "Current item: " + item.Name;
        evaluation.SetItem(item, settings.League);
        SearchButton.IsEnabled = item != null;
        if (item == null)
        {
            ItemName.Text = "No supported item found";
            ItemMeta.Text = $"Hover an item in POE2 and press {activeShortcut.Label}.";
            ItemDetails.Clear();
            Notice.Text = "Expected English POE2 item text.";
            SearchStatus.Text = "No item to search.";
            return;
        }
        ItemName.Text = item.Name;
        ItemMeta.Text = $"{item.Rarity} · {item.ItemClass}";
        ItemDetails.Text = item.Details; MainItemCard.Item = item;
        PopulateWorkspace(item);
        SearchStatus.Text = item.Rarity is "Rare" or "Magic" ? "Live comparable offers appear in the evaluation window. Select modifiers to narrow the comparison." : "Estimates are indicative. Compare variants and rolls before pricing your item.";
        Notice.Text = "Item ready to search";
    }
    private void OpenTrade(object sender, RoutedEventArgs e) => SearchCurrentItem();
    private void SearchCurrentItem()
    {
        if (currentItem == null) { Notice.Text = "Check an item first."; return; }
        try
        {
            string url = TradeSearch.BuildUrl(currentItem, settings.League, new TradeFilters(FilterBase.Text, FilterRarity.IsChecked == true, ReadMinimum(FilterLevel.Text), ReadMinimum(FilterQuality.Text), FilterCorrupted.IsChecked));
            if (!smoke) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            Notice.Text = "Trade search opened · sign in on the trade website if prompted";
        }
        catch (ArgumentException ex) { SearchStatus.Text = ex.Message; Notice.Text = ex.Message; }
        catch (Win32Exception) { Notice.Text = "Could not open your browser. Check your default browser and retry Search item."; }
    }

    private void RefreshWatchlist()
    {
        WatchList.ItemsSource = settings.Watchlist.OrderByDescending(x => x.Price).ThenBy(x => x.Name).ToList();
        EmptyWatch.Visibility = settings.Watchlist.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        MarketLabel.Text = $"{(string.IsNullOrWhiteSpace(settings.League) ? "Set your league in Settings" : settings.League)} · {settings.Currency}";
        expedition.Refresh(settings, currentArea);
    }
    private void WatchSelected(object sender, SelectionChangedEventArgs e)
    {
        selected = WatchList.SelectedItem as WatchEntry;
        RemoveButton.IsEnabled = selected != null;
        if (selected != null) { RewardName.Text = selected.Name; RewardPrice.Text = selected.Price?.ToString(CultureInfo.CurrentCulture) ?? ""; }
    }
    private void SaveReward(object sender, RoutedEventArgs e)
    {
        string name = RewardName.Text.Trim();
        if (name.Length == 0) { Notice.Text = "Enter a reward name first."; RewardName.Focus(); return; }
        decimal? price = null;
        if (!string.IsNullOrWhiteSpace(RewardPrice.Text))
        {
            if (!decimal.TryParse(RewardPrice.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var value) || value < 0)
            { Notice.Text = "Enter a positive price, zero, or leave the price blank."; return; }
            if (string.IsNullOrWhiteSpace(settings.League)) { Notice.Text = "Set and save your league in Settings before entering prices."; return; }
            price = value;
        }
        var entry = selected ?? settings.Watchlist.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (entry == null) { entry = new WatchEntry(); settings.Watchlist.Add(entry); }
        entry.Name = name; entry.Price = price; entry.Updated = DateTimeOffset.UtcNow;
        RefreshWatchlist();
        RewardName.Clear(); RewardPrice.Clear(); selected = null;
        if (Persist()) Notice.Text = "Reward saved · values are your manual reference prices";
    }
    private void RemoveReward(object sender, RoutedEventArgs e)
    {
        if (selected == null) return;
        settings.Watchlist.Remove(selected);
        selected = null;
        RefreshWatchlist(); RewardName.Clear(); RewardPrice.Clear();
        if (Persist()) Notice.Text = "Reward removed";
    }
    private void PinChanged(object sender, RoutedEventArgs e) { settings.Pinned = PinCheck.IsChecked == true; Persist(); }
    private void ApplyOverlayOpacity(double value)
    {
        Opacity = 1;
        evaluation.Opacity = value;
        expedition.Opacity = value;
    }
    private void OpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        settings.Opacity = e.NewValue;
        ApplyOverlayOpacity(e.NewValue);
        Persist();
    }
    private void TabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ready && ReferenceEquals(e.Source, Tabs)) ApplyOverlayOpacity(OpacitySlider.Value);
        if (ready && ReferenceEquals(e.Source, Tabs) && Tabs.SelectedItem == PricesTab && !smoke) _ = LoadBrowserPrices();
        if (ready && ReferenceEquals(e.Source,Tabs))
        {
            if(Tabs.SelectedItem==ExpeditionTab) RenderShortlist();
            if(Tabs.SelectedItem==GuidesTab) LoadGuide(sender,e);
            if(Tabs.SelectedItem==RuneTab) { runeHelper ??= new RuneHelperWindow(economy,smoke) { Resources=Resources }; runeHelper.Embed(RuneHost,settings.League); }
        }
    }
    private void BrowseLog(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Select POE2 Client.txt", Filter = "Game log (*.txt)|*.txt|All files (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) LogPathBox.Text = dialog.FileName;
    }
    private void SaveSettings(object sender, RoutedEventArgs e)
    {
        string path = LogPathBox.Text.Trim();
        if (!File.Exists(path)) { Notice.Text = "Log file not found. Browse to POE2's logs\\Client.txt."; return; }
        if (CurrencyBox.SelectedItem is not string chosenCurrency || string.IsNullOrWhiteSpace(chosenCurrency)) { Notice.Text = "Choose the currency used for reference prices."; CurrencyBox.Focus(); return; }
        var shortcut = CheckHotkey.Parse(HotkeyBox.Text);
        if (shortcut == null) { Notice.Text = "Choose a modifier plus letter, number or F-key. Ctrl+C and the overlay shortcuts are reserved."; return; }
        if (!ApplyShortcut(shortcut)) return;
        string chosenLeague = LeagueBox.SelectedValue as string ?? "";
        if (string.IsNullOrWhiteSpace(chosenLeague)) { Notice.Text = "Choose your league from the list before saving."; LeagueBox.Focus(); return; }
        ResaleCandidates.Text = "Price-check equipment to refresh market candidates."; OpportunityRows.ItemsSource = null; OpportunityStatus.Text = "";
        bool changedMarket = settings.League != chosenLeague || settings.Currency != chosenCurrency;
        if (changedMarket) foreach (var entry in settings.Watchlist) entry.Price = null;
        if (settings.LogPath != path)
        {
            tail = new LogTail(path);
            currentArea = null;
            AreaLabel.Text = "Waiting for your next area";
        }
        if (changedMarket) { useImportedListings = false; ResetQuote(); GuideRows.ItemsSource = null; GuideStatus.Text = "League changed. Load reward prices again."; guideRequest?.Cancel(); }
        settings.LogPath = path; settings.League = chosenLeague; settings.Currency = chosenCurrency;
        evaluation.SetPreferredCurrency(chosenCurrency);
        settings.AutoShow = AutoCheck.IsChecked == true; settings.Opacity = OpacitySlider.Value;
        settings.PriceCheckHotkey = activeShortcut.Label;
        UpdateShortcutLabels();
        RefreshWatchlist();
        if (Persist()) Notice.Text = changedMarket ? "Settings saved · old reference prices cleared" : "Settings saved";
        if (!smoke) PollLog();
    }
    private void CaptureHotkey(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Tab) return;
        e.Handled = true;
        var mods = Keyboard.Modifiers;
        uint modifiers = ((mods & ModifierKeys.Alt) != 0 ? 1u : 0) | ((mods & ModifierKeys.Control) != 0 ? 2u : 0) | ((mods & ModifierKeys.Shift) != 0 ? 4u : 0);
        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        if ((mods & ModifierKeys.Windows) != 0 || !(vk is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A or >= 0x70 and <= 0x7B)) return;
        var candidate = new CheckHotkey(modifiers, vk);
        if (CheckHotkey.Parse(candidate.Label) is { } valid) HotkeyBox.Text = valid.Label;
        else Notice.Text = "Use Alt, Ctrl or Shift with a letter, number or function key. Overlay shortcuts and Ctrl+C are reserved.";
    }
    private void SetLeagueChoices(System.Collections.Generic.IReadOnlyList<LeagueOption> options, string selection)
    {
        LeagueBox.ItemsSource = Leagues.WithSaved(options, selection);
        LeagueBox.SelectedValue = selection;
    }
    private async void RefreshLeaguesClick(object sender, RoutedEventArgs e) => await LoadLeaguesAsync();
    private async Task LoadLeaguesAsync()
    {
        if (!RefreshLeaguesButton.IsEnabled) return;
        RefreshLeaguesButton.IsEnabled = false;
        LeagueStatus.Text = "Updating league list…";
        try
        {
            var result = await leagueService.LoadAsync(lifetime.Token);
            string chosen = LeagueBox.SelectedValue as string ?? settings.League;
            leagueOptions = result.Items;
            SetLeagueChoices(leagueOptions, chosen);
            LeagueStatus.Text = result.Status + (string.IsNullOrEmpty(chosen) ? " · select a league" : "");
        }
        catch (OperationCanceledException) { }
        finally { if (!exiting) RefreshLeaguesButton.IsEnabled = true; }
    }
    private bool ApplyShortcut(CheckHotkey next)
    {
        if (smoke) { activeShortcut = next; return true; }
        if (checkRegistered && next == activeShortcut) return true;
        int nextId = checkHotkeyId == 1 ? 4 : 1;
        if (!Native.RegisterHotKey(handle, nextId, next.Modifiers | 0x4000, next.Key))
        { Notice.Text = $"{next.Label} is already in use. Choose another shortcut; your previous shortcut is unchanged."; return false; }
        if (checkRegistered) Native.UnregisterHotKey(handle, checkHotkeyId);
        checkRegistered = true; checkHotkeyId = nextId; activeShortcut = next;
        return true;
    }
    private void UpdateShortcutLabels()
    {
        CheckInstructions.Text = $"Hover an item in POE2 and press {activeShortcut.Label}. Exile Lens searches and shows prices in the evaluation window.";
        ShortcutSummary.Text = $"{activeShortcut.Label}   Check hovered item\nCtrl+Alt+O   Show / hide overlay\nCtrl+Alt+L   Toggle click-through";
    }
    private bool Persist()
    {
        if (smoke) return true;
        try { settings.Left = Left; settings.Top = Top; SettingsStore.Save(settings); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Notice.Text = "Could not save settings. Changes remain in memory for this session."; return false; }
    }

    public void RunSmokeTest()
    {
        Directory.CreateDirectory("artifacts");
        if (Tabs.Items[0] != HomeTab || Tabs.SelectedItem != HomeTab) throw new Exception("Home is not the default first page");
        Tabs.SelectedItem = ItemTab;
        double originalLeft = Left, originalTop = Top;
        TitleDragHandle.RaiseEvent(new DragDeltaEventArgs(40, 25) { RoutedEvent = Thumb.DragDeltaEvent });
        if (Math.Abs(Left - originalLeft - 40) > .1 || Math.Abs(Top - originalTop - 25) > .1) throw new Exception("Title drag did not move the window");
        SetLocked(true);
        TitleDragHandle.RaiseEvent(new DragDeltaEventArgs(10, 10) { RoutedEvent = Thumb.DragDeltaEvent });
        if (Math.Abs(Left - originalLeft - 40) > .1 || Math.Abs(Top - originalTop - 25) > .1) throw new Exception("Locked window moved");
        SetLocked(false);
        Left = originalLeft; Top = originalTop;
        WindowPlacement.Verify();
        TrayMenuPlacement.Verify();
        Capture("artifacts/overlay-empty.png");
        ApplyArea(new AreaEntry("ExpeditionLogBook_Atoll", 80, "1"));
        if (!expedition.IsVisible || Tabs.SelectedItem != ItemTab || !AreaLabel.Text.Contains("Atoll")) throw new Exception("Separate Logbook activation failed");
        ApplyArea(new AreaEntry("HideoutBlankUrban", 65, "1"));
        if (expedition.IsVisible || !IsVisible) throw new Exception("Departure did not hide only Expedition");
        ShowManual();
        settings.League = "Smoke-test league";
        RewardName.Text = "Test reward (fixture)"; RewardPrice.Text = "12.5";
        SaveReward(this, new RoutedEventArgs());
        if (settings.Watchlist.Single().Price != 12.5m) throw new Exception("Watchlist save failed");
        Tabs.SelectedItem = ExpeditionTab; Capture("artifacts/overlay-watchlist.png");
        ApplyArea(new AreaEntry("ExpeditionLogBook_Atoll", 80, "2"));
        Capture("artifacts/expedition-guide.png", expedition);
        ApplyArea(new AreaEntry("HideoutBlankUrban", 65, "1"));
        SetLocked(true);
        if ((Native.GetWindowLong(handle, -20) & 0x20) == 0) throw new Exception("Click-through style missing");
        SetLocked(false);
        if ((Native.GetWindowLong(handle, -20) & 0x20) != 0) throw new Exception("Click-through unlock failed");
        ShowItem(ItemParser.Parse("Item Class: Rings\nRarity: Rare\nDoom Circle\nGold Ring\n--------\nItem Level: 80\n--------\n+30 to maximum Life"));
        if (ItemName.Text != "Doom Circle") throw new Exception("Item view failed");
        Tabs.SelectedItem = ItemTab; Capture("artifacts/overlay-item.png");
        evaluation.SetItem(ItemParser.Parse("Item Class: Body Armours\nRarity: Unique\nForgotten Warden\nPrimal Markings\n--------\nItem Level: 84\nQuality: +20%\nEvasion Rating: 1338\nEnergy Shield: 409\n--------\n36% increased Armour, Evasion and Energy Shield (implicit)\n--------\n280% increased Evasion and Energy Shield\nCompanions have 36% increased maximum Life\n28 total to Attributes\nCorrupted"), "UI fixture");
        evaluation.Show();
        evaluation.SetQuote("TEST DATA · display verification only", new[] {
            new EconomyRow("Forgotten Warden", "Primal Markings", "Example variant A", 1.32m, "Divine Orb", 12, true),
            new EconomyRow("Forgotten Warden", "Primal Markings", "Example variant B", 2.38m, "Divine Orb", 8, true)
        });
        Capture("artifacts/evaluation-window.png", evaluation);
        evaluation.Width = 480; evaluation.Height = 520; Capture("artifacts/evaluation-small.png", evaluation);
        evaluation.VerifyPriceWorkflow();
        evaluation.Width = 600;
        evaluation.SetItem(ItemParser.Parse(File.ReadAllText("tests/Fixtures/quarterstaff.txt")), "UI fixture");
        evaluation.SetQuote("Rare-item pricing integration not connected");
        evaluation.VerifyLocalFilters(); evaluation.VerifyPinSnapshot();
        Capture("artifacts/evaluation-quarterstaff.png", evaluation);
        var fixtureTime = DateTimeOffset.UtcNow;
        var fixtureItem = ItemParser.Parse(File.ReadAllText("tests/Fixtures/quarterstaff.txt"))!;
        var fixtureData = new ListingDocument("UI fixture", "UI TEST DATA", fixtureTime,
            Enumerable.Range(1, 3).Select(i => new ImportedListing(i.ToString(), "test-seller-" + i, new ListingPrice(i * 10, "Exalted Orb"), fixtureItem.Details, fixtureTime, true, true)).ToArray());
        var fixtureMarket = ComparableMarket.Parse(System.Text.Json.JsonSerializer.Serialize(fixtureData), fixtureTime);
        evaluation.SetComparableResult(fixtureMarket.Search(new ComparableRequest(fixtureItem, "UI fixture", "Exalted Orb", Array.Empty<PriceConstraint>(), true, true, null), fixtureTime));
        evaluation.VerifyContentSizing(); Capture("artifacts/full-weapon-stats.png",evaluation); evaluation.SetComparableResult(fixtureMarket.Search(new ComparableRequest(fixtureItem, "UI fixture", "Exalted Orb", Array.Empty<PriceConstraint>(), true, true, null), fixtureTime)); evaluation.VerifyListingComparison(w => Capture("artifacts/listing-comparison.png", w)); evaluation.VerifyScrollableLayout(); Capture("artifacts/evaluation-layout-small.png", evaluation); evaluation.Width = 660; evaluation.Height = 820; Capture("artifacts/evaluation-comparables.png", evaluation);
        evaluation.SetItem(ItemParser.Parse(File.ReadAllText("tests/Fixtures/quarterstaff.txt")), "UI fixture");
        evaluation.Hide();
        PinComparison(this, new RoutedEventArgs());
        TrackLoot(this, new RoutedEventArgs());
        TrackLoot(this, new RoutedEventArgs());
        if (loot.Single().Quantity != 2 || !ComparisonLeft.Text.Contains("Doom Circle")) throw new Exception("Comparison or loot failed");
        VerifySessionPersistence();
        Tabs.SelectedItem = ComparisonTab; Capture("artifacts/overlay-compare.png");
        Tabs.SelectedItem = SessionTab; Capture("artifacts/overlay-session.png");
        Tabs.SelectedItem = GuidesTab; Capture("artifacts/overlay-guides.png");
        VerifyRuneHelper();
        evaluation.VerifyQol((path,window)=>Capture(path,window));
        var countBeforeNavigation=recent.Count; var checksBeforeNavigation=sessionChecks;
        var savedCurrent=currentItem;
        ShowItem(ItemParser.Parse(File.ReadAllText("tests/Fixtures/magic-crimson-amulet.txt")));
        NavigateRecent(1);
        if(currentItem?.Details!=savedCurrent?.Details || recent.Count!=countBeforeNavigation+1 || sessionChecks!=checksBeforeNavigation+1) throw new Exception("History navigation changed session counts or selected wrong item");
        NavigateRecent(-1);
        if(currentItem?.BaseType!="Crimson Amulet") throw new Exception("Forward navigation failed");
        Tabs.SelectedItem = MarketTab;
        PlanBuy.Text = "10"; PlanCost.Text = "2"; PlanSale.Text = "30"; PlanChance.Text = "50"; PlanFailure.Text = "0";
        CalculateOpportunity(this,new RoutedEventArgs());
        if (!PlanResult.Text.Contains("Expected net: 3")) throw new Exception("Market expected value calculation failed");
        Capture("artifacts/market-tools.png");

        Tabs.SelectedItem = SettingsTab;
        SetLeagueChoices(new[] { new LeagueOption("friendly-id", "Friendly league name") }, "friendly-id");
        UpdateLayout(); LeagueBox.ApplyTemplate();
        var closedText = VisualText(LeagueBox);
        if (!closedText.Contains("Friendly league name") || closedText.Contains("LeagueOption")) throw new Exception("Selected league does not render its display name");
        LeagueBox.IsDropDownOpen = true; UpdateLayout();
        var popup = (Popup)LeagueBox.Template.FindName("PART_Popup", LeagueBox);
        popup.Child?.Measure(new Size(LeagueBox.ActualWidth, 250));
        if (popup.Child != null) popup.Child.Arrange(new Rect(new Point(), popup.Child.DesiredSize));
        popup.Child?.UpdateLayout();
        if (popup.Child == null || !VisualText(popup.Child).Contains("Friendly league name")) throw new Exception("League dropdown rows do not render display names");
        LeagueBox.IsDropDownOpen = false;
        SetLeagueChoices(Leagues.Bundled, "Forbidden Rites");
        OpacitySlider.Value = .6;
        if (Opacity != 1 || Math.Abs(evaluation.Opacity - .6) > .001 || Math.Abs(expedition.Opacity - .6) > .001 || settings.Opacity != .6) throw new Exception("Overlay opacity affected the normal main window or missed an overlay");
        Capture("artifacts/overlay-settings.png");
        Tabs.SelectedItem = ItemTab;
        if (Opacity != 1) throw new Exception("Main window should remain opaque outside Settings");
        Tabs.SelectedItem = SettingsTab;
        Width = 600; Height = 560; Capture("artifacts/settings-small.png");
        Width = 720; Height = 760;
        var testLog = Path.GetTempFileName();
        try
        {
            LogPathBox.Text = testLog;
            LeagueBox.SelectedValue = "Standard";
            CurrencyBox.SelectedItem = "Divine Orb";
            if (CurrencyBox.IsEditable || CurrencyBox.SelectedItem as string != "Divine Orb") throw new Exception("Currency dropdown selection failed");
            HotkeyBox.Text = "Ctrl+Shift+Q";
            SaveSettings(this, new RoutedEventArgs());
            if (settings.Watchlist.Single().Price != null || settings.Currency != "Divine Orb") throw new Exception("Market change retained incompatible prices");
            if (settings.PriceCheckHotkey != "Ctrl+Shift+Q" || !CheckInstructions.Text.Contains("Ctrl+Shift+Q")) throw new Exception("Shortcut setting did not apply");
            if (settings.League != "Standard" || LeagueBox.IsEditable) throw new Exception("League dropdown selection not saved");
        }
        finally { File.Delete(testLog); }
        Width = 600; Height = 560; Tabs.SelectedItem = ItemTab; Capture("artifacts/overlay-small.png");
        evaluation.SetItem(ItemParser.Parse("Item Class: Support Gems\nRarity: Gem\nTacati's Ire\n--------\nSupport, Lineage, Chaos\n--------\nCategory: Tacati's Ire\nRequires: Level 65\nSupport Requirements: +5 Dex\n--------\nSupports Skills which can cause Damaging Hits. Poison inflicted with Supported Skills deals Damage faster the higher your Rage.\n--------\nPoisons from Supported Skills deal Damage 2% faster per Rage\n--------\nHe almost saved the Vaal. His unique poison made it past\nthe Queen's cupbearers; he had only to direct his anger...\nbut in her presence, he could feel naught but lust.\n--------\nPlace into a Skill's Support Gem socket in the Skills Panel to apply its effects to that Skill.")!, "Gem UI fixture"); evaluation.Width = 740; evaluation.Height = 800; evaluation.Show(); evaluation.UpdateLayout(); Capture("artifacts/lineage-gem.png",evaluation); CaptureShowcase();
    }
    private static string VisualText(DependencyObject root)
    {
        string text = root is TextBlock block ? block.Text : "";
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) text += " " + VisualText(VisualTreeHelper.GetChild(root, i));
        return text;
    }
    private void Capture(string path, Window? target = null)
    {
        var window = target ?? this;
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
