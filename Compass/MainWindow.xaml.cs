using System.ComponentModel;
using System.Windows;
using Compass.Services;
using Compass.Views;

namespace Compass;

public partial class MainWindow : Window
{
    private string _current = "";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AutoStartLabel.Text = StartupManager.IsEnabled()
                ? "✓ Auto-starts with Windows.\nCloses to the tray — it keeps running in the background."
                : "Runs in the background. Closing the window keeps it alive in the tray.";
            Navigate("today");
        };
    }

    public void Navigate(string key)
    {
        _current = key;
        MainContent.Content = key switch
        {
            "today" => new TodayView(this),
            "deadlines" => new DeadlinesView(this),
            "assistant" => new AssistantView(this),
            "playbooks" => new PlaybooksView(this),
            "inbox" => new InboxView(this),
            "settings" => new SettingsView(this),
            _ => new TodayView(this),
        };
    }

    public void Refresh() => Navigate(_current);

    public void OpenAddDeadline()
    {
        var dlg = new AddDeadlineDialog { Owner = this };
        if (dlg.ShowDialog() == true)
            Navigate(_current == "deadlines" ? "deadlines" : "today");
    }

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    // Closing the window does NOT quit — it hides to the tray so reminders keep firing.
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private void NavToday_Click(object sender, RoutedEventArgs e) => Navigate("today");
    private void NavDeadlines_Click(object sender, RoutedEventArgs e) => Navigate("deadlines");
    private void NavAssistant_Click(object sender, RoutedEventArgs e) => Navigate("assistant");
    private void NavPlaybooks_Click(object sender, RoutedEventArgs e) => Navigate("playbooks");
    private void NavInbox_Click(object sender, RoutedEventArgs e) => Navigate("inbox");
    private void NavSettings_Click(object sender, RoutedEventArgs e) => Navigate("settings");
    private void Add_Click(object sender, RoutedEventArgs e) => OpenAddDeadline();
}
