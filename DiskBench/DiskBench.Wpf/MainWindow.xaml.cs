using System.Windows;
using System.Windows.Input;

namespace DiskBench.Wpf;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // DataContext is set by App.xaml.cs - do NOT create another MainViewModel here
    }
    
    public void ApplyQuickModeSize()
    {
        // Compact mode for context menu launch
        Width = 600;
        Height = 450;
        MinWidth = 500;
        MinHeight = 350;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            Maximize_Click(sender, e);
        }
        else
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized 
            ? WindowState.Normal 
            : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        // Cancel any running benchmark
        if (DataContext is MainViewModel vm && vm.IsRunning)
        {
            vm.StopBenchmarkCommand.Execute(null);
        }
        Close();
    }
}
