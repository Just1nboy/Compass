using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Compass.Models;
using Compass.Services;
using Compass.Ui;

namespace Compass.Views;

public partial class TodayView : UserControl
{
    private readonly MainWindow _win;

    public TodayView(MainWindow win)
    {
        InitializeComponent();
        _win = win;
        Loaded += (_, _) => Build();
    }

    private void Build()
    {
        DateTime now = DateTime.Now;
        Greeting.Text = $"{GreetPart(now.Hour)}, {Environment.UserName}.";
        TodayDate.Text = "Today is " + now.ToString("dddd, d MMMM yyyy");

        AttentionPanel.Children.Clear();
        UpcomingPanel.Children.Clear();

        var active = DataStore.Instance.Data.Deadlines.Where(d => !d.Deleted && !d.Completed).OrderBy(d => d.Due).ToList();

        var attention = active.Where(d =>
            d.Due <= now.AddHours(48) ||
            (d.Critical && !d.Acknowledged && d.Due <= now.AddDays(7))).ToList();

        var attentionIds = attention.Select(d => d.Id).ToHashSet();

        var upcoming = active.Where(d =>
            !attentionIds.Contains(d.Id) && d.Due > now && d.Due <= now.AddDays(14)).ToList();

        if (attention.Count == 0)
            AttentionPanel.Children.Add(EmptyNote("Nothing urgent right now. 🎉  You're on top of it."));
        else
            foreach (var d in attention)
                AttentionPanel.Children.Add(UiKit.DeadlineCard(d, _win.Refresh));

        if (upcoming.Count == 0)
            UpcomingPanel.Children.Add(EmptyNote("Nothing in the next two weeks. Add anything you know is coming."));
        else
            foreach (var d in upcoming)
                UpcomingPanel.Children.Add(UiKit.DeadlineCard(d, _win.Refresh));
    }

    private static string GreetPart(int hour) => hour switch
    {
        >= 5 and < 12 => "Good morning",
        >= 12 and < 17 => "Good afternoon",
        >= 17 and < 21 => "Good evening",
        _ => "Still up"
    };

    private Border EmptyNote(string text) => new()
    {
        Background = UiKit.B("Surface"),
        BorderBrush = UiKit.B("Line"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(16, 14, 16, 14),
        Child = new TextBlock { Text = text, Foreground = UiKit.B("Muted"), FontSize = 13, TextWrapping = TextWrapping.Wrap }
    };

    private void Capture_Click(object sender, RoutedEventArgs e) => AddCapture();

    private void CaptureBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) AddCapture();
    }

    private void AddCapture()
    {
        string text = CaptureBox.Text.Trim();
        if (text.Length == 0) return;
        DataStore.Instance.Data.Inbox.Insert(0, new CaptureItem { Text = text });
        DataStore.Instance.Save();
        CaptureBox.Clear();
        _win.Navigate("inbox");
    }
}
