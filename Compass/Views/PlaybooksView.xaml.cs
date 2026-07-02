using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Compass.Models;
using Compass.Services;
using Compass.Ui;

namespace Compass.Views;

public partial class PlaybooksView : UserControl
{
    private readonly MainWindow _win;

    public PlaybooksView(MainWindow win)
    {
        InitializeComponent();
        _win = win;
        Loaded += (_, _) => Build();
    }

    private void Build()
    {
        Root.Children.Clear();
        foreach (var pb in DataStore.Instance.Data.Playbooks.Where(p => !p.Deleted))
            Root.Children.Add(BuildPlaybookCard(pb));
    }

    private Border BuildPlaybookCard(Playbook pb)
    {
        var outer = new StackPanel();

        // Header
        var header = new StackPanel { Margin = new Thickness(16, 14, 16, 6) };
        header.Children.Add(new TextBlock
        {
            Text = pb.Title,
            Foreground = UiKit.B("Text"),
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
        });
        header.Children.Add(new TextBlock
        {
            Text = pb.Description,
            Foreground = UiKit.B("Muted"),
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 8),
        });

        var progress = new TextBlock { Foreground = UiKit.B("Accent"), FontSize = 12, FontWeight = FontWeights.SemiBold };
        var toggle = new Button
        {
            Style = (Style)Application.Current.Resources["Ghost"],
            Content = "Show steps",
            FontSize = 12,
            Padding = new Thickness(10, 6, 10, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0),
        };

        var details = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(16, 6, 16, 14) };

        void UpdateProgress()
        {
            int done = pb.Steps.Count(s => s.Done);
            progress.Text = $"{done} / {pb.Steps.Count} done";
        }
        UpdateProgress();

        toggle.Click += (_, _) =>
        {
            bool show = details.Visibility != Visibility.Visible;
            details.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            toggle.Content = show ? "Hide steps" : "Show steps";
        };

        header.Children.Add(progress);
        header.Children.Add(toggle);

        int n = 1;
        foreach (var step in pb.Steps)
            details.Children.Add(BuildStep(pb, step, n++, UpdateProgress));

        outer.Children.Add(header);
        outer.Children.Add(details);

        return new Border
        {
            Background = UiKit.B("Surface"),
            BorderBrush = UiKit.B("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(0, 0, 0, 14),
            Child = outer,
        };
    }

    private FrameworkElement BuildStep(Playbook pb, PlaybookStep step, int number, Action onChange)
    {
        var grid = new Grid { Margin = new Thickness(0, 6, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var check = new CheckBox { IsChecked = step.Done, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 10, 0) };

        var body = new StackPanel();
        var text = new TextBlock
        {
            Text = $"{number}.  {step.Text}",
            Foreground = step.Done ? UiKit.B("Muted") : UiKit.B("Text"),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
        };
        if (step.Done) text.TextDecorations = TextDecorations.Strikethrough;
        body.Children.Add(text);

        if (!string.IsNullOrWhiteSpace(step.Ask))
            body.Children.Add(new TextBlock
            {
                Text = "❓ Ask:  " + step.Ask,
                Foreground = UiKit.B("Amber"),
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 0),
            });

        if (!string.IsNullOrWhiteSpace(step.WhereToFind))
            body.Children.Add(new TextBlock
            {
                Text = "📍 Find it:  " + step.WhereToFind,
                Foreground = UiKit.B("Muted"),
                FontSize = 12.5,
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0),
            });

        var toDeadline = new Button
        {
            Style = (Style)Application.Current.Resources["Ghost"],
            Content = "＋ turn into a deadline",
            FontSize = 11.5,
            Padding = new Thickness(9, 5, 9, 5),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 6, 0, 0),
        };
        toDeadline.Click += (_, _) =>
        {
            var dlg = new AddDeadlineDialog(step.Text) { Owner = _win };
            if (dlg.ShowDialog() == true) _win.Navigate("deadlines");
        };
        body.Children.Add(toDeadline);

        check.Checked += (_, _) => { step.Done = true; pb.Touch(); DataStore.Instance.Save(); text.TextDecorations = TextDecorations.Strikethrough; text.Foreground = UiKit.B("Muted"); onChange(); };
        check.Unchecked += (_, _) => { step.Done = false; pb.Touch(); DataStore.Instance.Save(); text.TextDecorations = null; text.Foreground = UiKit.B("Text"); onChange(); };

        Grid.SetColumn(check, 0);
        Grid.SetColumn(body, 1);
        grid.Children.Add(check);
        grid.Children.Add(body);
        return grid;
    }
}
