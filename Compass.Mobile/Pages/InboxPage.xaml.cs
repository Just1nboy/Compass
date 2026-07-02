using Compass.Models;
using Compass.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace Compass.Mobile.Pages;

public partial class InboxPage : ContentPage
{
    public InboxPage()
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
        ListPanel.Children.Clear();
        var items = MobileStore.Instance.Data.Inbox.Where(i => !i.Deleted).ToList();

        if (items.Count == 0)
        {
            ListPanel.Children.Add(new Label
            {
                Text = "Inbox is empty. Good — nothing floating around unrecorded.",
                TextColor = MobileUi.C("Muted"),
                FontSize = 13,
            });
            return;
        }

        foreach (var item in items) ListPanel.Children.Add(BuildRow(item));
    }

    private View BuildRow(CaptureItem item)
    {
        var body = new VerticalStackLayout { Spacing = 2, HorizontalOptions = LayoutOptions.Fill };
        body.Add(new Label { Text = item.Text, TextColor = MobileUi.C("TextC"), FontSize = 14 });
        body.Add(new Label { Text = "captured " + item.CreatedAt.ToString("ddd d MMM, h:mm tt"), TextColor = MobileUi.C("Muted"), FontSize = 11 });

        var del = new Button
        {
            Text = "✕",
            BackgroundColor = MobileUi.C("Surface2"),
            TextColor = MobileUi.C("Muted"),
            FontSize = 13,
            CornerRadius = 8,
            WidthRequest = 44,
            VerticalOptions = LayoutOptions.Center,
        };
        del.Clicked += (_, _) =>
        {
            item.MarkDeleted();
            MobileStore.Instance.Save();
            Build();
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Padding = new Thickness(14, 10),
        };
        grid.Add(body, 0, 0);
        grid.Add(del, 1, 0);

        return new Border
        {
            BackgroundColor = MobileUi.C("Surface"),
            Stroke = new SolidColorBrush(MobileUi.C("Line")),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Padding = 0,
            Content = grid,
        };
    }

    private void Add_Clicked(object? sender, EventArgs e)
    {
        string text = (Box.Text ?? "").Trim();
        if (text.Length == 0) return;
        MobileStore.Instance.Data.Inbox.Insert(0, new CaptureItem { Text = text });
        MobileStore.Instance.Save();
        Box.Text = "";
        Build();
    }
}
