using System.Windows;

namespace DiskBench.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Parse command line arguments
        var args = e.Args;
        var viewModel = new MainViewModel();

        if (args.Length > 0)
        {
            viewModel.ParseCommandLineArgs(args);
        }

        var mainWindow = new MainWindow
        {
            DataContext = viewModel
        };

        // Apply compact size for quick mode
        if (viewModel.IsQuickMode)
        {
            mainWindow.ApplyQuickModeSize();
        }

        mainWindow.Show();

        // Auto-start if command line specified
        if (viewModel.AutoStart)
        {
            viewModel.StartBenchmarkCommand.Execute(null);
        }
    }
}
