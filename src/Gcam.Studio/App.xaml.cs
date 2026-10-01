using System.Windows;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Core.ViewModels;
using Gcam.Studio.Services;
using Gcam.Studio.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Gcam.Studio;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _services = new ServiceCollection()
            .AddSingleton<IAcquisitionService, SimulationService>()
            .AddSingleton<ThemeService>()
            .AddSingleton<IThemeService>(sp => sp.GetRequiredService<ThemeService>())
            .AddSingleton<MainViewModel>()
            .AddSingleton<MainWindow>()
            .BuildServiceProvider();

        var theme = _services.GetRequiredService<ThemeService>();
        var window = _services.GetRequiredService<MainWindow>();
        window.DataContext = _services.GetRequiredService<MainViewModel>();
        window.SourceInitialized += (_, _) => theme.ApplyTitleBar(window);

        // The layout is designed for 1440×900; on smaller work areas start maximised instead of overflowing.
        if (SystemParameters.WorkArea.Width < window.Width || SystemParameters.WorkArea.Height < window.Height)
            window.WindowState = WindowState.Maximized;

        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
