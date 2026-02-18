using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VoiceSynthWPF;

public partial class TopPanel
{
    public TopPanel()
    {
        InitializeComponent();
        
        TopBorder.MouseDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Left)
                MainWindow.Instance!.DragMove();
        };
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => MainWindow.Instance!.WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Environment.Exit(0);
}