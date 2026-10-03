using System.Diagnostics;
using System.Text.Json;
using GlanceMD.Core;
using GlanceMD_App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;

namespace GlanceMD_App;

public sealed partial class MainPage : Page
{
    private readonly IDocumentLoader _loader = new DocumentLoader();
    private readonly IMarkdownRenderer _renderer = new MarkdownRenderer();
    private readonly IResourcePolicy _resourcePolicy = new ResourcePolicy();
    private readonly SemaphoreSlim _openGate = new(1, 1);
    private readonly AppSettingsService _settingsService = new();
    private AppSettings _settings = AppSettings.Default;
    private LoadedDocument? _document;
    private RenderedDocument? _rendered;
    private CancellationTokenSource? _openCancellation;
    private FileSystemWatcher? _watcher;
    private DispatcherTimer? _reloadTimer;
    private bool _documentReady;
    private double _zoom = 1.0;

    public MainPage()
    {
        InitializeComponent();
        _settings = _settingsService.Load();
        _zoom = Math.Clamp(_settings.Zoom, 0.5, 3.0);
        RequestedTheme = _settings.Theme switch
        {
            "Dark" => ElementTheme.Dark,
            "Light" => ElementTheme.Light,
            _ => ElementTheme.Default
        };
        RefreshRecentFiles();
        Loaded += MainPage_Loaded;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializeWebViewAsync();
        if (App.PendingFilePath is { Length: > 0 } pending)
        {
            App.PendingFilePath = null;
            await OpenDocumentAsync(pending);
        }
    }

    public Task OpenDocumentFromActivationAsync(string path) => OpenDocumentAsync(path);

    private async Task InitializeWebViewAsync()
    {
        try
        {
            await DocumentView.EnsureCoreWebView2Async();
            var core = DocumentView.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsStatusBarEnabled = true;
            core.SetVirtualHostNameToFolderMapping(
                "app.glancemd.local",
                Path.Combine(AppContext.BaseDirectory, "WebAssets"),
                CoreWebView2HostResourceAccessKind.DenyCors);
            core.AddWebResourceRequestedFilter(
                "https://resource.glancemd.local/*",
                CoreWebView2WebResourceContext.Image);
            core.WebResourceRequested += Core_WebResourceRequested;
            core.WebMessageReceived += Core_WebMessageReceived;
            core.NewWindowRequested += Core_NewWindowRequested;
            core.DownloadStarting += Core_DownloadStarting;
            core.PermissionRequested += Core_PermissionRequested;
        }
        catch (Exception exception)
        {
            ShowError("The document viewer could not be initialized.", exception.Message);
        }
    }

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        picker.ViewMode = PickerViewMode.List;
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".md");
        picker.FileTypeFilter.Add(".markdown");
        picker.FileTypeFilter.Add(".mdown");
        picker.FileTypeFilter.Add(".mkd");
        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            await OpenDocumentAsync(file.Path);
        }
    }

    private async Task OpenDocumentAsync(string path)
    {
        _openCancellation?.Cancel();
        _openCancellation?.Dispose();
        _openCancellation = new CancellationTokenSource();
        var cancellationToken = _openCancellation.Token;

        await _openGate.WaitAsync(cancellationToken);
        try
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            var loaded = await _loader.LoadAsync(new DocumentRequest(path), cancellationToken);
            var rendered = await _renderer.RenderAsync(loaded, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            _document = loaded;
            _rendered = rendered;
            _documentReady = false;
            OutlineList.ItemsSource = rendered.Outline;
            var theme = ActualTheme == ElementTheme.Dark ? "dark" : "light";
            var webDocument = rendered with { Html = HtmlResourceRewriter.RewriteImageSources(rendered.Html) };
            DocumentView.NavigateToString(HtmlShellBuilder.Build(webDocument, loaded.DisplayName, theme));
            WelcomePanel.Visibility = Visibility.Collapsed;
            DocumentView.Visibility = Visibility.Visible;
            ReloadButton.IsEnabled = true;
            FindButton.IsEnabled = true;
            PrintButton.IsEnabled = false;
            App.SetWindowTitle($"{loaded.DisplayName} — GlanceMD");
            ConfigureWatcher(loaded.FullPath);
            if (_settings.RecentHistoryEnabled)
            {
                _settings = _settings with
                {
                    RecentDocuments = RecentDocuments.Add(_settings.RecentDocuments, loaded.FullPath, DateTimeOffset.UtcNow)
                };
                _settingsService.Save(_settings);
                RefreshRecentFiles();
            }
            if (rendered.Warnings.Count > 0)
            {
                ShowStatus(string.Join(" ", rendered.Warnings.Select(warning => warning.Message)), InfoBarSeverity.Warning);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (DocumentLoadException exception)
        {
            ShowError(exception.Message);
        }
        catch (Exception exception)
        {
            ShowError("GlanceMD could not render this document.", exception.Message);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            _openGate.Release();
        }
    }

    private void ConfigureWatcher(string path)
    {
        _watcher?.Dispose();
        _reloadTimer?.Stop();
        _reloadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _reloadTimer.Tick += async (_, _) =>
        {
            _reloadTimer.Stop();
            if (_document is not null)
            {
                await OpenDocumentAsync(_document.FullPath);
            }
        };

        _watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _watcher.Changed += Watcher_Changed;
        _watcher.Renamed += Watcher_Changed;
        _watcher.Deleted += Watcher_Deleted;
    }

    private void Watcher_Changed(object sender, FileSystemEventArgs e) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            _reloadTimer?.Stop();
            _reloadTimer?.Start();
        });

    private void Watcher_Deleted(object sender, FileSystemEventArgs e) =>
        DispatcherQueue.TryEnqueue(() => ShowStatus(
            "The source file was deleted. The last rendered view is still available.",
            InfoBarSeverity.Warning));

    private async void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (_document is not null)
        {
            await OpenDocumentAsync(_document.FullPath);
        }
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (!_documentReady)
        {
            ShowStatus("Wait for diagrams to finish rendering before printing.", InfoBarSeverity.Informational);
            return;
        }

        DocumentView.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.System);
    }

    private void Find_Click(object sender, RoutedEventArgs e)
    {
        FindBar.Visibility = Visibility.Visible;
        FindTextBox.Focus(FocusState.Programmatic);
    }

    private async void FindTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        await SendScriptAsync("window.glanceMD.find", FindTextBox.Text, false);

    private async void FindNext_Click(object sender, RoutedEventArgs e) =>
        await SendScriptAsync("window.glanceMD.find", FindTextBox.Text, false);

    private async void FindPrevious_Click(object sender, RoutedEventArgs e) =>
        await SendScriptAsync("window.glanceMD.find", FindTextBox.Text, true);

    private void CloseFind_Click(object sender, RoutedEventArgs e) => CloseFind();

    private void CloseFind()
    {
        FindBar.Visibility = Visibility.Collapsed;
        DocumentView.Focus(FocusState.Programmatic);
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom + 0.1);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom - 0.1);
    private void ResetZoom_Click(object sender, RoutedEventArgs e) => SetZoom(1.0);

    private void SetZoom(double value)
    {
        _zoom = Math.Clamp(value, 0.5, 3.0);
        _ = SendScriptAsync("window.glanceMD.setZoom", _zoom);
        _settings = _settings with { Zoom = _zoom };
        _settingsService.Save(_settings);
        ShowStatus($"Zoom: {_zoom:P0}", InfoBarSeverity.Informational);
    }

    private void Outline_Click(object sender, RoutedEventArgs e)
    {
        var show = OutlinePane.Visibility != Visibility.Visible;
        OutlinePane.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        OutlineColumn.Width = show ? new GridLength(260) : new GridLength(0);
    }

    private async void OutlineList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OutlineList.SelectedItem is HeadingItem heading)
        {
            await SendScriptAsync("window.glanceMD.scrollTo", heading.Anchor);
        }
    }

    private async void Theme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string theme })
        {
            return;
        }

        RequestedTheme = theme switch
        {
            "Dark" => ElementTheme.Dark,
            "Light" => ElementTheme.Light,
            _ => ElementTheme.Default
        };
        _settings = _settings with { Theme = theme };
        _settingsService.Save(_settings);
        var webTheme = ActualTheme == ElementTheme.Dark ? "dark" : "light";
        await SendScriptAsync("window.glanceMD.setTheme", webTheme);
    }

    private async void RecentList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RecentDocument recent)
        {
            if (File.Exists(recent.FullPath))
            {
                await OpenDocumentAsync(recent.FullPath);
            }
            else
            {
                _settings = _settings with
                {
                    RecentDocuments = _settings.RecentDocuments.Where(item => item.FullPath != recent.FullPath).ToArray()
                };
                _settingsService.Save(_settings);
                RefreshRecentFiles();
                ShowError("That recent file no longer exists.");
            }
        }
    }

    private void ClearRecent_Click(object sender, RoutedEventArgs e)
    {
        _settings = _settings with { RecentDocuments = [] };
        _settingsService.Save(_settings);
        RefreshRecentFiles();
    }

    private void RefreshRecentFiles()
    {
        RecentList.ItemsSource = _settings.RecentDocuments;
        var visibility = _settings.RecentHistoryEnabled && _settings.RecentDocuments.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        RecentList.Visibility = visibility;
        RecentHeading.Visibility = visibility;
    }

    private async void About_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "About GlanceMD",
            Content = $"GlanceMD {GetType().Assembly.GetName().Version}\n\nA secure, read-only Markdown and Mermaid viewer.\n\n.NET {Environment.Version}",
            CloseButtonText = "Close"
        };
        await dialog.ShowAsync();
    }

    private async void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var control = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (e.Key == VirtualKey.Escape && FindBar.Visibility == Visibility.Visible)
        {
            CloseFind();
            e.Handled = true;
        }
        else if (control && e.Key == VirtualKey.O)
        {
            Open_Click(sender, e);
            e.Handled = true;
        }
        else if (control && e.Key == VirtualKey.F)
        {
            Find_Click(sender, e);
            e.Handled = true;
        }
        else if (control && e.Key == VirtualKey.P)
        {
            Print_Click(sender, e);
            e.Handled = true;
        }
        else if (control && e.Key == VirtualKey.R)
        {
            Reload_Click(sender, e);
            e.Handled = true;
        }
    }

    private void Page_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Open Markdown file";
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        if (items.Count == 1 && items[0] is StorageFile file)
        {
            await OpenDocumentAsync(file.Path);
        }
        else
        {
            ShowError("Drop exactly one Markdown file.");
        }
    }

    private void DocumentView_NavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (args.Uri == "about:blank" || args.Uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!args.Uri.StartsWith("https://app.glancemd.local", StringComparison.OrdinalIgnoreCase))
        {
            args.Cancel = true;
            _ = HandleLinkAsync(args.Uri);
        }
    }

    private async Task HandleLinkAsync(string reference)
    {
        if (_document is null)
        {
            return;
        }

        var decision = _resourcePolicy.Evaluate(_document.FullPath, reference);
        if (decision.Kind == ResourceDecisionKind.AllowedLocalMarkdown && decision.ResolvedPath is not null)
        {
            await OpenDocumentAsync(decision.ResolvedPath);
        }
        else if (decision.Kind == ResourceDecisionKind.RequiresConfirmation && decision.ResolvedPath is not null &&
                 Uri.TryCreate(decision.ResolvedPath, UriKind.Absolute, out var uri))
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Open external destination?",
                Content = decision.ResolvedPath,
                PrimaryButtonText = "Open",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(uri);
            }
        }
        else
        {
            ShowStatus(decision.Reason, InfoBarSeverity.Warning);
        }
    }

    private async void Core_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var json = JsonDocument.Parse(e.WebMessageAsJson);
            var root = json.RootElement;
            var type = root.GetProperty("type").GetString();
            switch (type)
            {
                case "ready":
                    _documentReady = true;
                    PrintButton.IsEnabled = true;
                    break;
                case "copy-text":
                    CopyText(root.GetProperty("text").GetString() ?? string.Empty);
                    break;
                case "copy-svg":
                    await CopySvgAsBitmapAsync(root.GetProperty("svg").GetString() ?? string.Empty);
                    break;
                case "link":
                    await HandleLinkAsync(root.GetProperty("href").GetString() ?? string.Empty);
                    break;
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
        }
    }

    private static void CopyText(string text)
    {
        var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        package.SetText(text);
        Clipboard.SetContent(package);
    }

    private static async Task CopySvgAsBitmapAsync(string svg)
    {
        var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        var stream = new InMemoryRandomAccessStream();
        await using (var output = stream.AsStreamForWrite())
        await using (var writer = new StreamWriter(output, leaveOpen: true))
        {
            await writer.WriteAsync(svg);
            await writer.FlushAsync();
        }
        stream.Seek(0);
        package.SetData("image/svg+xml", RandomAccessStreamReference.CreateFromStream(stream));
        Clipboard.SetContent(package);
    }

    private void Core_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        _ = HandleLinkAsync(e.Uri);
    }

    private void Core_WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (_document is null || !Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri))
        {
            e.Response = CreateResourceResponse(null, 404, "Not Found", "text/plain");
            return;
        }

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var requestedReference = query["src"] ?? string.Empty;
        var decision = _resourcePolicy.Evaluate(_document.FullPath, requestedReference);
        if (decision.Kind != ResourceDecisionKind.AllowedLocalImage || decision.ResolvedPath is null)
        {
            e.Response = CreateResourceResponse(null, 403, "Blocked", "text/plain");
            return;
        }

        try
        {
            var bytes = File.ReadAllBytes(decision.ResolvedPath);
            var stream = new InMemoryRandomAccessStream();
            using (var output = stream.AsStreamForWrite())
            {
                output.Write(bytes);
                output.Flush();
            }
            stream.Seek(0);
            e.Response = CreateResourceResponse(stream, 200, "OK", GetImageContentType(decision.ResolvedPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            e.Response = CreateResourceResponse(null, 404, "Not Found", "text/plain");
        }
    }

    private CoreWebView2WebResourceResponse CreateResourceResponse(
        IRandomAccessStream? content,
        int statusCode,
        string reason,
        string contentType) =>
        DocumentView.CoreWebView2.Environment.CreateWebResourceResponse(
            content,
            statusCode,
            reason,
            $"Content-Type: {contentType}\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff");

    private static string GetImageContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };

    private void Core_DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e) => e.Cancel = true;

    private void Core_PermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e) =>
        e.State = CoreWebView2PermissionState.Deny;

    private async Task SendScriptAsync(string function, params object[] arguments)
    {
        if (DocumentView.CoreWebView2 is null)
        {
            return;
        }

        var values = string.Join(",", arguments.Select(argument => JsonSerializer.Serialize(argument)));
        await DocumentView.CoreWebView2.ExecuteScriptAsync($"{function}({values})");
    }

    private void ShowError(string message, string? details = null) =>
        ShowStatus(details is null ? message : $"{message} {details}", InfoBarSeverity.Error);

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusBar.Message = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;
    }
}
