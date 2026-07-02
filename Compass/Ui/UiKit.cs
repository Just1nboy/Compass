using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Compass.Models;
using Compass.Services;

namespace Compass.Ui;

/// <summary>Helpers that build styled controls in code, so the views stay small.</summary>
public static class UiKit
{
    public static SolidColorBrush B(string key) => (SolidColorBrush)Application.Current.Resources[key];

    public static SolidColorBrush UrgencyBrush(string urgency) => urgency switch
    {
        "overdue" => B("Red"),
        "urgent" => B("Red"),
        "soon" => B("Amber"),
        "upcoming" => B("Accent"),
        "done" => B("Green"),
        _ => B("Muted"),
    };

    public static Border Chip(string text, Brush fg)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(30, ((SolidColorBrush)fg).Color.R,
                                                            ((SolidColorBrush)fg).Color.G, ((SolidColorBrush)fg).Color.B)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = text, Foreground = fg, FontSize = 11, FontWeight = FontWeights.SemiBold }
        };
    }

    private static Button SmallButton(string text, string styleKey, RoutedEventHandler onClick)
    {
        var btn = new Button
        {
            Content = text,
            Style = (Style)Application.Current.Resources[styleKey],
            Margin = new Thickness(0, 0, 8, 0),
            FontSize = 12,
            Padding = new Thickness(10, 6, 10, 6),
        };
        btn.Click += onClick;
        return btn;
    }

    /// <summary>A full card for a single deadline, with actions.</summary>
    public static Border DeadlineCard(Deadline d, Action refresh)
    {
        DateTime now = DateTime.Now;
        string urgency = Humanize.Urgency(d.Due, now, d.Completed);
        Brush accent = UrgencyBrush(urgency);

        var stripe = new Border { Background = accent, CornerRadius = new CornerRadius(3, 0, 0, 3), Width = 6 };

        var content = new StackPanel { Margin = new Thickness(16, 12, 14, 12) };

        // Title row + kind chip
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        var title = new TextBlock
        {
            Text = d.Title,
            Foreground = B("Text"),
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 460,
        };
        if (d.Completed)
        {
            title.TextDecorations = TextDecorations.Strikethrough;
            title.Foreground = B("Muted");
        }
        titleRow.Children.Add(title);
        var kindChip = Chip(d.Kind, B("Muted"));
        kindChip.Margin = new Thickness(10, 0, 0, 0);
        titleRow.Children.Add(kindChip);
        if (d.Critical && !d.Completed)
        {
            var c = Chip("CRITICAL", B("Red"));
            c.Margin = new Thickness(6, 0, 0, 0);
            titleRow.Children.Add(c);
        }
        content.Children.Add(titleRow);

        // Countdown
        content.Children.Add(new TextBlock
        {
            Text = d.Completed ? "Completed ✓" : Humanize.Countdown(d.Due, now),
            Foreground = d.Completed ? B("Green") : accent,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 6, 0, 2),
        });

        // Read-back (hard to misread)
        content.Children.Add(new TextBlock
        {
            Text = Humanize.ReadBack(d.Due),
            Foreground = B("Muted"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
        });

        if (!string.IsNullOrWhiteSpace(d.WhereToFind))
        {
            content.Children.Add(new TextBlock
            {
                Text = "Where to check: " + d.WhereToFind,
                Foreground = B("Muted"),
                FontStyle = FontStyles.Italic,
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap,
            });
        }
        if (!string.IsNullOrWhiteSpace(d.Notes))
        {
            content.Children.Add(new TextBlock
            {
                Text = d.Notes,
                Foreground = B("Muted"),
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap,
            });
        }

        // Actions
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        if (!d.Completed)
        {
            actions.Children.Add(SmallButton("✓ Done", "Primary", (_, _) =>
            {
                d.Completed = true;
                d.Touch();
                DataStore.Instance.Save();
                refresh();
            }));

            if (d.Critical && !d.Acknowledged)
            {
                actions.Children.Add(SmallButton("I've got this", "Ghost", (_, _) =>
                {
                    d.Acknowledged = true;
                    d.Touch();
                    DataStore.Instance.Save();
                    refresh();
                }));
            }
        }
        else
        {
            actions.Children.Add(SmallButton("Undo", "Ghost", (_, _) =>
            {
                d.Completed = false;
                d.Touch();
                DataStore.Instance.Save();
                refresh();
            }));
        }

        actions.Children.Add(SmallButton("Delete", "Ghost", (_, _) =>
        {
            if (MessageBox.Show($"Delete “{d.Title}”?", "Compass",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                d.MarkDeleted();
                DataStore.Instance.Save();
                refresh();
            }
        }));
        content.Children.Add(actions);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(stripe, 0);
        Grid.SetColumn(content, 1);
        grid.Children.Add(stripe);
        grid.Children.Add(content);

        return new Border
        {
            Background = B("Surface"),
            BorderBrush = B("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0, 0, 0, 10),
            Child = grid,
        };
    }
}
