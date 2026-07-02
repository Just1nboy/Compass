using System.Diagnostics;
using System.Windows;

namespace Compass.Views;

public partial class DeviceCodeWindow : Window
{
    private readonly string _url;
    private readonly string _code;

    public DeviceCodeWindow(string message, string url, string code)
    {
        InitializeComponent();
        MessageBox.Text = message;
        _url = url;
        _code = code;
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(string.IsNullOrWhiteSpace(_url)
                ? "https://microsoft.com/devicelogin" : _url) { UseShellExecute = true });
        }
        catch { }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(_code); CopyBtn.Content = "Copied ✓"; } catch { }
    }
}
