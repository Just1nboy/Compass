using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Compass.Models;
using Compass.Services;
using Compass.Ui;

namespace Compass.Views;

public partial class InboxView : UserControl
{
    private readonly MainWindow _win;

    public InboxView(MainWindow win)
    {
        InitializeComponent();
        _win = win;
        Loaded += (_, _) => Build();
    }

    private void Build()
    {
        ListPanel.Children.Clear();
        var items = DataStore.Instance.Data.Inbox.Where(i => !i.Deleted).ToList();

        if (items.Count == 0)
        {
            ListPanel.Children.Add(new TextBlock
            {
                Text = "Inbox is empty. Good — nothing floating around unrecorded.",
                Foreground = UiKit.B("Muted"),
                FontSize = 13,
            });
            return;
        }

        foreach (var item in items)
            ListPanel.Children.Add(BuildRow(item));
    }

    private Border BuildRow(CaptureItem item)
    {
        var grid = new Grid { Margin = new Thickness(14, 10, 12, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var body = new StackPanel();
        body.Children.Add(new TextBlock
        {
            Text = item.Text,
            Foreground = item.Processed ? UiKit.B("Muted") : UiKit.B("Text"),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            TextDecorations = item.Processed ? TextDecorations.Strikethrough : null,
        });
        body.Children.Add(new TextBlock
        {
            Text = "captured " + item.CreatedAt.ToString("ddd d MMM, h:mm tt"),
            Foreground = UiKit.B("Muted"),
            FontSize = 11,
            Margin = new Thickness(0, 3, 0, 0),
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var makeBtn = new Button
        {
            Style = (Style)Application.Current.Resources["Ghost"],
            Content = "→ deadline",
            FontSize = 12,
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 8, 0),
        };
        makeBtn.Click += (_, _) =>
        {
            var dlg = new AddDeadlineDialog(item.Text) { Owner = _win };
            if (dlg.ShowDialog() == true)
            {
                item.Processed = true;
                item.Touch();
                DataStore.Instance.Save();
                _win.Navigate("deadlines");
            }
        };

        var delBtn = new Button
        {
            Style = (Style)Application.Current.Resources["Ghost"],
            Content = "✕",
            FontSize = 12,
            Padding = new Thickness(9, 6, 9, 6),
        };
        delBtn.Click += (_, _) =>
        {
            item.MarkDeleted();
            DataStore.Instance.Save();
            _win.Refresh();
        };

        actions.Children.Add(makeBtn);
        actions.Children.Add(delBtn);

        Grid.SetColumn(body, 0);
        Grid.SetColumn(actions, 1);
        grid.Children.Add(body);
        grid.Children.Add(actions);

        return new Border
        {
            Background = UiKit.B("Surface"),
            BorderBrush = UiKit.B("Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0, 0, 0, 10),
            Child = grid,
        };
    }

    private void Add_Click(object sender, RoutedEventArgs e) => AddItem();
    private void Box_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) AddItem(); }

    private void AddItem()
    {
        string text = Box.Text.Trim();
        if (text.Length == 0) return;
        DataStore.Instance.Data.Inbox.Insert(0, new CaptureItem { Text = text });
        DataStore.Instance.Save();
        Box.Clear();
        _win.Refresh();
    }
}
