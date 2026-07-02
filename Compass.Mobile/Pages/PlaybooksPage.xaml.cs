using Compass.Models;
using Compass.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace Compass.Mobile.Pages;

public partial class PlaybooksPage : ContentPage
{
    public PlaybooksPage()
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
        Root.Children.Clear();
        foreach (var pb in MobileStore.Instance.Data.Playbooks.Where(p => !p.Deleted))
            Root.Children.Add(BuildCard(pb));
    }

    private View BuildCard(Playbook pb)
    {
        var outer = new VerticalStackLayout { Padding = new Thickness(14, 12), Spacing = 4 };

        outer.Add(new Label { Text = pb.Title, TextColor = MobileUi.C("TextC"), FontSize = 17, FontAttributes = FontAttributes.Bold });
        if (!string.IsNullOrWhiteSpace(pb.Description))
            outer.Add(new Label { Text = pb.Description, TextColor = MobileUi.C("Muted"), FontSize = 12.5 });

        var progress = new Label { TextColor = MobileUi.C("Accent"), FontSize = 12, FontAttributes = FontAttributes.Bold };
        outer.Add(progress);

        var steps = new VerticalStackLayout { Spacing = 10, Margin = new Thickness(0, 8, 0, 0), IsVisible = false };

        void UpdateProgress() => progress.Text = $"{pb.Steps.Count(s => s.Done)} / {pb.Steps.Count} done";
        UpdateProgress();

        var toggle = new Button
        {
            Text = "Show steps",
            BackgroundColor = MobileUi.C("Surface2"),
            TextColor = MobileUi.C("TextC"),
            FontSize = 13,
            CornerRadius = 8,
            HorizontalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 6, 0, 0),
        };
        toggle.Clicked += (_, _) =>
        {
            steps.IsVisible = !steps.IsVisible;
            toggle.Text = steps.IsVisible ? "Hide steps" : "Show steps";
        };
        outer.Add(toggle);

        int n = 1;
        foreach (var step in pb.Steps)
            steps.Add(BuildStep(pb, step, n++, UpdateProgress));
        outer.Add(steps);

        return new Border
        {
            BackgroundColor = MobileUi.C("Surface"),
            Stroke = new SolidColorBrush(MobileUi.C("Line")),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Padding = 0,
            Content = outer,
        };
    }

    private View BuildStep(Playbook pb, PlaybookStep step, int number, Action onChange)
    {
        var body = new VerticalStackLayout { Spacing = 2, HorizontalOptions = LayoutOptions.Fill };
        var text = new Label
        {
            Text = $"{number}.  {step.Text}",
            TextColor = step.Done ? MobileUi.C("Muted") : MobileUi.C("TextC"),
            FontSize = 14,
        };
        if (step.Done) text.TextDecorations = TextDecorations.Strikethrough;
        body.Add(text);

        if (!string.IsNullOrWhiteSpace(step.Ask))
            body.Add(new Label { Text = "❓ Ask:  " + step.Ask, TextColor = MobileUi.C("Amber"), FontSize = 12.5 });
        if (!string.IsNullOrWhiteSpace(step.WhereToFind))
            body.Add(new Label { Text = "📍 Find it:  " + step.WhereToFind, TextColor = MobileUi.C("Muted"), FontSize = 12.5 });

        var check = new CheckBox { IsChecked = step.Done, Color = MobileUi.C("Accent"), VerticalOptions = LayoutOptions.Start };
        check.CheckedChanged += (_, e) =>
        {
            step.Done = e.Value;
            pb.Touch();
            MobileStore.Instance.Save();
            text.TextDecorations = step.Done ? TextDecorations.Strikethrough : TextDecorations.None;
            text.TextColor = step.Done ? MobileUi.C("Muted") : MobileUi.C("TextC");
            onChange();
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
            }
        };
        grid.Add(check, 0, 0);
        grid.Add(body, 1, 0);
        return grid;
    }
}
