using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GlanceMD_App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    public static string? PendingFilePath { get; set; }

    /// <summary>
    /// The main application window. Use <c>App.Window</c> from any class that needs
    /// the window reference (for dialogs, pickers, interop, etc.).
    /// </summary>
    public static Window Window { get; private set; } = null!;

    /// <summary>
    /// The UI thread dispatcher. Use <c>App.DispatcherQueue</c> to marshal calls
    /// to the UI thread. Fully qualified to avoid CS0104 ambiguity with
    /// <see cref="Windows.System.DispatcherQueue"/>.
    /// </summary>
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    /// <summary>
    /// The native window handle (HWND). Use for file pickers,
    /// <c>DataTransferManager</c>, and any WinRT interop that requires
    /// <c>InitializeWithWindow</c>.
    /// </summary>
    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();
        AppInstance.GetCurrent().Activated += Current_Activated;
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        PendingFilePath = GetActivatedFilePath(AppInstance.GetCurrent().GetActivatedEventArgs())
            ?? ParseFileArgument(args.Arguments);
        Window = new MainWindow();
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Window.Activate();
    }

    private void Current_Activated(object? sender, AppActivationArguments args)
    {
        var path = GetActivatedFilePath(args);
        if (path is null)
        {
            return;
        }

        DispatcherQueue?.TryEnqueue(async () =>
        {
            if (Window is MainWindow window)
            {
                await window.MainPage.OpenDocumentFromActivationAsync(path);
                window.Activate();
            }
            else
            {
                PendingFilePath = path;
            }
        });
    }

    private static string? GetActivatedFilePath(AppActivationArguments arguments)
    {
        if (arguments.Kind == ExtendedActivationKind.File &&
            arguments.Data is IFileActivatedEventArgs fileArguments &&
            fileArguments.Files.FirstOrDefault() is StorageFile file)
        {
            return file.Path;
        }

        return null;
    }

    public static void SetWindowTitle(string title)
    {
        if (Window is MainWindow window)
        {
            window.Title = title;
            window.AppWindow.Title = title;
        }
    }

    private static string? ParseFileArgument(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return null;
        }

        var candidate = arguments.Trim().Trim('"');
        return File.Exists(candidate) ? Path.GetFullPath(candidate) : null;
    }
}
