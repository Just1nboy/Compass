using Compass.Models;
using Compass.Mobile.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace Compass.Mobile;

/// <summary>Shared builders for the phone UI (deadline cards, chips).</summary>
public static class MobileUi
{
    public static Color C(string key) => (Color)Application.Current!.Resources[key];

    public static Color UrgencyColor(string u) => u switch
    {
        "overdue" or "urgent" => C("Red"),
        "soon" => C("Amber"),
        "upcoming" => C("Accent"),
        "done" => C("Green"),
        _ => C("Muted"),
    };

    public static Border Card(View content) => new()
    {
        BackgroundColor = C("Surface"),
        Stroke = new SolidColorBrush(C("Line")),
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
        Padding = 0,
        Content = content,
    };

    private static Button MiniButton(string text, Color bg, Color fg, EventHandler onClick)
    {
        var b = new Button
        {
            Text = text,
            BackgroundColor = bg,
            TextColor = fg,
            FontSize = 13,
            CornerRadius = 8,
            Padding = new Thickness(12, 6),
            HeightRequest = 38,
        };
        b.Clicked += onClick;
        return b;
    }

    public static View DeadlineCard(Deadline d, Action refresh)
    {
        DateTime now = DateTime.Now;
        string urgency = Humanize.Urgency(d.Due, now, d.Completed);
        Color accent = UrgencyColor(urgency);

        var content = new VerticalStackLayout { Padding = new Thickness(14, 12, 12, 12), Spacing = 4 };

        var title = new Label
        {
            Text = d.Title,
            TextColor = d.Completed ? C("Muted") : C("TextC"),
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
        };
        if (d.Completed) title.TextDecorations = TextDecorations.Strikethrough;
        content.Add(title);

        content.Add(new Label
        {
            Text = d.Completed ? "Completed ✓" : Humanize.Countdown(d.Due, now),
            TextColor = d.Completed ? C("Green") : accent,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
        });

        content.Add(new Label
        {
            Text = Humanize.ReadBack(d.Due),
            TextColor = C("Muted"),
            FontSize = 13,
        });

        if (!string.IsNullOrWhiteSpace(d.WhereToFind))
            content.Add(new Label { Text = "Where to check: " + d.WhereToFind, TextColor = C("Muted"), FontSize = 12 });

        var actions = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        if (!d.Completed)
        {
            actions.Add(MiniButton("✓ Done", C("Accent"), Colors.White, (_, _) =>
            {
                d.Completed = true; d.Touch(); MobileStore.Instance.Save(); refresh();
            }));
            if (d.Critical && !d.Acknowledged)
                actions.Add(MiniButton("Got it", C("Surface2"), C("TextC"), (_, _) =>
                {
                    d.Acknowledged = true; d.Touch(); MobileStore.Instance.Save(); refresh();
                }));
        }
        else
        {
            actions.Add(MiniButton("Undo", C("Surface2"), C("TextC"), (_, _) =>
            {
                d.Completed = false; d.Touch(); MobileStore.Instance.Save(); refresh();
            }));
        }
        actions.Add(MiniButton("Delete", C("Surface2"), C("Muted"), async (_, _) =>
        {
            bool ok = await Shell.Current.DisplayAlert("Delete?", $"Delete “{d.Title}”?", "Delete", "Cancel");
            if (ok) { d.MarkDeleted(); MobileStore.Instance.Save(); refresh(); }
        }));
        content.Add(actions);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
            }
        };
        grid.Add(new BoxView { Color = accent, WidthRequest = 5 }, 0, 0);
        grid.Add(content, 1, 0);

        return Card(grid);
    }
}
