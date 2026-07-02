using System.Windows;
using System.Windows.Controls;
using Compass.Services;
using Compass.Ui;

namespace Compass.Views;

public partial class DeadlinesView : UserControl
{
    private readonly MainWindow _win;

    public DeadlinesView(MainWindow win)
    {
        InitializeComponent();
        _win = win;
        Loaded += (_, _) => Build();
    }

    private void Build()
    {
        ActivePanel.Children.Clear();
        DonePanel.Children.Clear();

        var all = DataStore.Instance.Data.Deadlines.Where(d => !d.Deleted).ToList();
        var active = all.Where(d => !d.Completed).OrderBy(d => d.Due).ToList();
        var done = all.Where(d => d.Completed).OrderByDescending(d => d.Due).ToList();

        if (active.Count == 0)
        {
            ActivePanel.Children.Add(new TextBlock
            {
                Text = "No active deadlines. Tap “Add deadline” to track your next exam or due date.",
                Foreground = UiKit.B("Muted"),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10),
            });
        }
        else
        {
            foreach (var d in active)
                ActivePanel.Children.Add(UiKit.DeadlineCard(d, _win.Refresh));
        }

        DoneHeader.Visibility = done.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var d in done)
            DonePanel.Children.Add(UiKit.DeadlineCard(d, _win.Refresh));
    }

    private void Add_Click(object sender, RoutedEventArgs e) => _win.OpenAddDeadline();
}
