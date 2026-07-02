using System.Windows;
using System.Windows.Controls;
using Compass.Models;
using Compass.Services;

namespace Compass.Views;

public partial class AddDeadlineDialog : Window
{
    public AddDeadlineDialog(string? prefillTitle = null, DateTime? prefillDue = null,
        string? prefillKind = null, bool prefillCritical = false)
    {
        InitializeComponent();

        KindBox.ItemsSource = new[] { "Exam", "Assignment", "Admin", "Deadline", "Other" };
        KindBox.SelectedIndex = 0;

        for (int h = 1; h <= 12; h++) HourBox.Items.Add(h.ToString());
        for (int m = 0; m < 60; m++) MinBox.Items.Add(m.ToString("00"));
        AmPmBox.ItemsSource = new[] { "AM", "PM" };

        PrepBox.ItemsSource = new[]
        {
            "No prep reminder", "1 day before", "2 days before", "3 days before",
            "5 days before", "1 week before", "2 weeks before"
        };
        PrepBox.SelectedIndex = 0;

        // Default: tomorrow 9:00 AM, unless a specific time was passed in.
        DateTime seed = prefillDue ?? DateTime.Today.AddDays(1).AddHours(9);
        SetDateTime(seed);

        if (!string.IsNullOrWhiteSpace(prefillTitle))
            TitleBox.Text = prefillTitle;
        if (!string.IsNullOrWhiteSpace(prefillKind) && ((string[])KindBox.ItemsSource).Contains(prefillKind))
            KindBox.SelectedItem = prefillKind;
        if (prefillCritical)
            CriticalBox.IsChecked = true;

        DateBox.SelectedDateChanged += (_, _) => OnInputChanged();
        HourBox.SelectionChanged += (_, _) => OnInputChanged();
        MinBox.SelectionChanged += (_, _) => OnInputChanged();
        AmPmBox.SelectionChanged += (_, _) => OnInputChanged();

        Loaded += (_, _) => { TitleBox.Focus(); UpdatePreview(); };
    }

    private void SetDateTime(DateTime dt)
    {
        DateBox.SelectedDate = dt.Date;
        int hour12 = ((dt.Hour + 11) % 12) + 1;
        HourBox.SelectedItem = hour12.ToString();
        MinBox.SelectedItem = dt.Minute.ToString("00");
        AmPmBox.SelectedItem = dt.Hour < 12 ? "AM" : "PM";
    }

    private void OnInputChanged()
    {
        // Any change to the date/time invalidates a prior confirmation — force a re-check.
        if (ConfirmBox.IsChecked == true)
            ConfirmBox.IsChecked = false;
        UpdatePreview();
    }

    private bool TryBuildDateTime(out DateTime dt)
    {
        dt = default;
        if (DateBox.SelectedDate is not DateTime date) return false;
        if (HourBox.SelectedItem is not string hs || !int.TryParse(hs, out int hour12)) return false;
        if (MinBox.SelectedItem is not string ms || !int.TryParse(ms, out int min)) return false;
        if (AmPmBox.SelectedItem is not string ap) return false;

        int hour24 = hour12 % 12;
        if (ap == "PM") hour24 += 12;

        dt = new DateTime(date.Year, date.Month, date.Day, hour24, min, 0);
        return true;
    }

    private void UpdatePreview()
    {
        if (TryBuildDateTime(out DateTime dt))
        {
            Preview.Text = Humanize.ReadBack(dt);
            PreviewCountdown.Text = Humanize.Countdown(dt, DateTime.Now);
        }
        else
        {
            Preview.Text = "Pick a date and time…";
            PreviewCountdown.Text = "";
        }
    }

    private void Confirm_Changed(object sender, RoutedEventArgs e)
    {
        SaveBtn.IsEnabled = ConfirmBox.IsChecked == true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string title = TitleBox.Text.Trim();
        if (title.Length == 0)
        {
            MessageBox.Show("Give it a name first (e.g. “Calculus final exam”).", "Compass",
                MessageBoxButton.OK, MessageBoxImage.Information);
            TitleBox.Focus();
            return;
        }
        if (!TryBuildDateTime(out DateTime due))
        {
            MessageBox.Show("Please pick a valid date and time.", "Compass",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        int prep = PrepBox.SelectedIndex switch
        {
            1 => 1, 2 => 2, 3 => 3, 4 => 5, 5 => 7, 6 => 14, _ => 0
        };

        var d = new Deadline
        {
            Title = title,
            Due = due,
            Kind = KindBox.SelectedItem as string ?? "Deadline",
            Critical = CriticalBox.IsChecked == true,
            PrepDays = prep,
            WhereToFind = WhereBox.Text.Trim(),
            Notes = NotesBox.Text.Trim(),
        };

        DataStore.Instance.Data.Deadlines.Add(d);
        DataStore.Instance.Save();

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
