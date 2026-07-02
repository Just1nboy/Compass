using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Compass.Models;
using Compass.Services;
using Compass.Ui;

namespace Compass.Views;

public partial class AssistantView : UserControl
{
    private readonly MainWindow _win;
    private readonly AiTasks _ai = new();
    private Playbook? _generated;
    private DispatcherTimer? _progress;

    private static readonly string[] ExampleSituations =
    {
        "I just got accepted to a university and don't know what to do next",
        "I need to register for next semester's courses",
        "I want to apply for a scholarship",
        "My final exams are coming up",
        "I need to renew my passport",
    };

    public AssistantView(MainWindow win)
    {
        InitializeComponent();
        _win = win;
        foreach (var ex in ExampleSituations)
        {
            var b = new Button
            {
                Content = ex,
                Style = (Style)Application.Current.Resources["Ghost"],
                Margin = new Thickness(0, 0, 8, 8),
                FontSize = 12,
                Padding = new Thickness(10, 6, 10, 6),
            };
            b.Click += (_, _) => SituationBox.Text = ex;
            Examples.Children.Add(b);
        }
    }

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        string situation = SituationBox.Text.Trim();
        if (situation.Length < 3)
        {
            Status.Text = "Type a situation first.";
            return;
        }
        if (!ClaudeService.IsAvailable())
        {
            MessageBox.Show(
                "The Claude Code CLI wasn't found. Install it and sign in with your Claude subscription, " +
                "then try again. (You can also set its path in Settings.)",
                "Compass — AI not available", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        GenerateBtn.IsEnabled = false;
        ResultCard.Visibility = Visibility.Collapsed;
        StartProgress();

        try
        {
            _generated = await _ai.GenerateChecklistAsync(situation);
            StopProgress();
            RenderResult(_generated);
            Status.Text = "✓ Done. Review it below, then save it if it's useful.";
        }
        catch (Exception ex)
        {
            StopProgress();
            Status.Text = "Couldn't generate a checklist — see the message.";
            MessageBox.Show(Window.GetWindow(this),
                "Couldn't generate a checklist:\n\n" + ex.Message,
                "Compass", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            GenerateBtn.IsEnabled = true;
        }
    }

    // A live counter so it's obvious Claude is working — this genuinely takes 20–40 seconds,
    // and a static label made it look frozen. Don't close the window while it counts.
    private void StartProgress()
    {
        var started = DateTime.Now;
        Status.Text = "Working on it… 0s  (this usually takes 20–40s — please keep this window open)";
        _progress?.Stop();
        _progress = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _progress.Tick += (_, _) =>
        {
            int s = (int)(DateTime.Now - started).TotalSeconds;
            Status.Text = $"Working on it… {s}s  (this usually takes 20–40s — please keep this window open)";
        };
        _progress.Start();
    }

    private void StopProgress()
    {
        _progress?.Stop();
        _progress = null;
    }

    private void RenderResult(Playbook pb)
    {
        ResultTitle.Text = pb.Title;
        ResultDesc.Text = pb.Description;
        ResultSteps.Children.Clear();

        int n = 1;
        foreach (var step in pb.Steps)
        {
            var box = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            box.Children.Add(new TextBlock
            {
                Text = $"{n++}.  {step.Text}",
                Foreground = UiKit.B("Text"),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            });
            if (!string.IsNullOrWhiteSpace(step.Ask))
                box.Children.Add(new TextBlock
                {
                    Text = "❓ Ask:  " + step.Ask,
                    Foreground = UiKit.B("Amber"),
                    FontSize = 12.5,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 0, 0),
                });
            if (!string.IsNullOrWhiteSpace(step.WhereToFind))
                box.Children.Add(new TextBlock
                {
                    Text = "📍 Find it:  " + step.WhereToFind,
                    Foreground = UiKit.B("Muted"),
                    FontSize = 12.5,
                    FontStyle = FontStyles.Italic,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0),
                });
            ResultSteps.Children.Add(box);
        }
        ResultCard.Visibility = Visibility.Visible;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_generated == null) return;
        DataStore.Instance.Data.Playbooks.Add(_generated);
        DataStore.Instance.Save();
        MessageBox.Show("Saved to Playbooks. You'll find it under 📖 Playbooks.",
            "Compass", MessageBoxButton.OK, MessageBoxImage.Information);
        _win.Navigate("playbooks");
    }
}
