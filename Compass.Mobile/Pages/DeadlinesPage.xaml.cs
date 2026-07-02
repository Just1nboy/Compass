using Compass.Mobile.Services;

namespace Compass.Mobile.Pages;

public partial class DeadlinesPage : ContentPage
{
    public DeadlinesPage()
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
        ActivePanel.Children.Clear();
        DonePanel.Children.Clear();

        var all = MobileStore.Instance.Data.Deadlines.Where(d => !d.Deleted).ToList();
        var active = all.Where(d => !d.Completed).OrderBy(d => d.Due).ToList();
        var done = all.Where(d => d.Completed).OrderByDescending(d => d.Due).ToList();

        if (active.Count == 0)
            ActivePanel.Children.Add(new Label
            {
                Text = "No active deadlines. Tap Add to track your next exam or due date.",
                TextColor = MobileUi.C("Muted"),
                FontSize = 13,
            });
        else
            foreach (var d in active) ActivePanel.Children.Add(MobileUi.DeadlineCard(d, Build));

        DoneHeader.IsVisible = done.Count > 0;
        foreach (var d in done) DonePanel.Children.Add(MobileUi.DeadlineCard(d, Build));
    }

    private async void Add_Clicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("add");
    }
}
