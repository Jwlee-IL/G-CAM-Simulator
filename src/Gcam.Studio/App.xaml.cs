using System.Windows;
using Gcam.Studio.Core.Services;
using Gcam.Studio.Services;
using Gcam.Studio.Core.ViewModels;
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
            .AddSingleton<ISimulationService, SimulationService>()
            .AddSingleton<MainViewModel>()
            .AddSingleton<MainWindow>()
            .BuildServiceProvider();

        var window = _services.GetRequiredService<MainWindow>();
        window.DataContext = _services.GetRequiredService<MainViewModel>();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
