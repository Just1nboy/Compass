using Compass.Models;
using Compass.Mobile.Services;

namespace Compass.Mobile.Pages;

public partial class TodayPage : ContentPage
{
    public TodayPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        MobileSync.Synced += Build;
        Build();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        MobileSync.Synced -= Build;
    }

    private async void Refresh_Refreshing(object? sender, EventArgs e)
    {
        await MobileSync.RunAsync();
        Build();
        Refresh.IsRefreshing = false;
    }

    private void Build()
    {
        DateTime now = DateTime.Now;
        Greeting.Text = GreetPart(now.Hour) + " 👋";
        TodayDate.Text = "Today is " + now.ToString("dddd, d MMMM yyyy");

        AttentionPanel.Children.Clear();
        UpcomingPanel.Children.Clear();

        var active = MobileStore.Instance.Data.Deadlines.Where(d => !d.Deleted && !d.Completed).OrderBy(d => d.Due).ToList();

        var attention = active.Where(d =>
            d.Due <= now.AddHours(48) ||
            (d.Critical && !d.Acknowledged && d.Due <= now.AddDays(7))).ToList();
        var attentionIds = attention.Select(d => d.Id).ToHashSet();
        var upcoming = active.Where(d => !attentionIds.Contains(d.Id) && d.Due > now && d.Due <= now.AddDays(14)).ToList();

        if (attention.Count == 0)
            AttentionPanel.Children.Add(EmptyNote("Nothing urgent right now. 🎉"));
        else
            foreach (var d in attention) AttentionPanel.Children.Add(MobileUi.DeadlineCard(d, Build));

        if (upcoming.Count == 0)
            UpcomingPanel.Children.Add(EmptyNote("Nothing in the next two weeks."));
        else
            foreach (var d in upcoming) UpcomingPanel.Children.Add(MobileUi.DeadlineCard(d, Build));
    }

    private static string GreetPart(int h) => h switch
    {
        >= 5 and < 12 => "Good morning",
        >= 12 and < 17 => "Good afternoon",
        >= 17 and < 21 => "Good evening",
        _ => "Still up",
    };

    private View EmptyNote(string text) => new Border
    {
        BackgroundColor = MobileUi.C("Surface"),
        Stroke = new SolidColorBrush(MobileUi.C("Line")),
        StrokeThickness = 1,
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(12) },
        Padding = new Thickness(14, 12),
        Content = new Label { Text = text, TextColor = MobileUi.C("Muted"), FontSize = 13 },
    };

    private void Capture_Clicked(object? sender, EventArgs e)
    {
        string text = (CaptureBox.Text ?? "").Trim();
        if (text.Length == 0) return;
        MobileStore.Instance.Data.Inbox.Insert(0, new CaptureItem { Text = text });
        MobileStore.Instance.Save();
        CaptureBox.Text = "";
    }
}
