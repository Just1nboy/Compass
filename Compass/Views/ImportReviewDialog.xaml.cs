using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Compass.Models;
using Compass.Services;
using Compass.Ui;

namespace Compass.Views;

public partial class ImportReviewDialog : Window
{
    private readonly EmailScan _scan;

    public ImportReviewDialog(EmailScan scan)
    {
        InitializeComponent();
        _scan = scan;
        Build();
    }

    private void Build()
    {
        List.Children.Clear();

        if (_scan.IsEmpty)
        {
            Subtitle.Text = _scan.EmailCount > 0
                ? $"Scanned {_scan.EmailCount} emails — nothing that needs action right now."
                : "No recent emails to scan.";
            List.Children.Add(new TextBlock
            {
                Text = "Nothing needing your attention was found. That's fine — your inbox is clear of action items.",
                Foreground = UiKit.B("Muted"),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }

        Subtitle.Text = $"Scanned {_scan.EmailCount} emails. Deadlines need the date checked before saving; " +
                        "action items go straight to your inbox to handle.";

        // ---- Deadlines ----
        if (_scan.Deadlines.Count > 0)
        {
            List.Children.Add(SectionHeader($"📅  Deadlines found ({_scan.Deadlines.Count})"));
            foreach (var d in _scan.Deadlines.OrderBy(i => i.Due))
                List.Children.Add(BuildDeadlineRow(d));
        }

        // ---- Action items ----
        if (_scan.Actions.Count > 0)
        {
            var header = SectionHeader($"✅  Things to handle ({_scan.Actions.Count})");
            header.Margin = new Thickness(0, 18, 0, 8);
            List.Children.Add(header);

            var addAll = new Button
            {
                Content = "Add all to my inbox",
                Style = (Style)Application.Current.Resources["Primary"],
                HorizontalAlignment = HorizontalAlignment.Left,
                FontSize = 12.5,
                Padding = new Thickness(12, 7, 12, 7),
                Margin = new Thickness(0, 0, 0, 10),
            };
            addAll.Click += (_, _) =>
            {
                int n = EmailImporter.AddActionsToInbox(_scan.Actions);
                addAll.Content = n > 0 ? $"Added {n} to inbox ✓" : "Already in inbox ✓";
                addAll.IsEnabled = false;
            };
            List.Children.Add(addAll);

            foreach (var a in _scan.Actions)
                List.Children.Add(BuildActionRow(a));
        }
    }

    private static TextBlock SectionHeader(string text) => new()
    {
        Text = text,
        Foreground = UiKit.B("Text"),
        FontSize = 15,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 0, 0, 10),
    };

    private Border BuildDeadlineRow(ExtractedDeadline item)
    {
        var panel = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };
        panel.Children.Add(new TextBlock { Text = item.Title, Foreground = UiKit.B("Text"), FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock
        {
            Text = item.Due != null ? Humanize.ReadBack(item.Due.Value) : item.DueIso,
            Foreground = UiKit.B("Muted"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        });

        var meta = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        meta.Children.Add(UiKit.Chip(item.Kind, UiKit.B("Muted")));
        if (item.Critical) meta.Children.Add(UiKit.Chip("critical", UiKit.B("Red")));
        string conf = item.Confidence >= 0.75 ? "high confidence" : item.Confidence >= 0.4 ? "medium" : "low — double-check";
        meta.Children.Add(UiKit.Chip(conf, item.Confidence >= 0.75 ? UiKit.B("Green") : UiKit.B("Amber")));
        panel.Children.Add(meta);

        if (!string.IsNullOrWhiteSpace(item.Source))
            panel.Children.Add(new TextBlock
            {
                Text = "from email: " + item.Source + (string.IsNullOrWhiteSpace(item.Account) ? "" : $"  ({item.Account})"),
                Foreground = UiKit.B("Muted"),
                FontStyle = FontStyles.Italic,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0),
            });

        var addBtn = new Button
        {
            Content = "Review & add →",
            Style = (Style)Application.Current.Resources["Primary"],
            HorizontalAlignment = HorizontalAlignment.Left,
            FontSize = 12.5,
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 10, 0, 0),
        };
        addBtn.Click += (_, _) =>
        {
            var dlg = new AddDeadlineDialog(item.Title, item.Due, item.Kind, item.Critical) { Owner = this };
            if (dlg.ShowDialog() == true) { addBtn.Content = "Added ✓"; addBtn.IsEnabled = false; }
        };
        panel.Children.Add(addBtn);

        return Card(panel);
    }

    private Border BuildActionRow(EmailAction a)
    {
        var panel = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };
        panel.Children.Add(new TextBlock { Text = a.Text, Foreground = UiKit.B("Text"), FontSize = 14, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(a.Source))
            panel.Children.Add(new TextBlock
            {
                Text = "from email: " + a.Source + (string.IsNullOrWhiteSpace(a.Account) ? "" : $"  ({a.Account})"),
                Foreground = UiKit.B("Muted"),
                FontStyle = FontStyles.Italic,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 0),
            });

        var add = new Button
        {
            Content = "Add to inbox",
            Style = (Style)Application.Current.Resources["Ghost"],
            HorizontalAlignment = HorizontalAlignment.Left,
            FontSize = 12.5,
            Padding = new Thickness(11, 6, 11, 6),
            Margin = new Thickness(0, 8, 0, 0),
        };
        add.Click += (_, _) =>
        {
            int n = EmailImporter.AddActionsToInbox(new[] { a });
            add.Content = n > 0 ? "Added ✓" : "Already there ✓";
            add.IsEnabled = false;
        };
        panel.Children.Add(add);

        return Card(panel);
    }

    private static Border Card(UIElement child) => new()
    {
        Background = UiKit.B("Surface"),
        BorderBrush = UiKit.B("Line"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(10),
        Margin = new Thickness(0, 0, 0, 10),
        Child = child,
    };

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
