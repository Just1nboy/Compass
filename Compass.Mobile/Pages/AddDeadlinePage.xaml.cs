using System.ComponentModel;
using Compass.Models;
using Compass.Mobile.Services;

namespace Compass.Mobile.Pages;

public partial class AddDeadlinePage : ContentPage
{
    public AddDeadlinePage()
    {
        InitializeComponent();

        KindBox.ItemsSource = new[] { "Exam", "Assignment", "Admin", "Deadline", "Other" };
        KindBox.SelectedIndex = 0;

        PrepBox.ItemsSource = new[]
        {
            "No prep reminder", "1 day before", "2 days before", "3 days before",
            "5 days before", "1 week before", "2 weeks before"
        };
        PrepBox.SelectedIndex = 0;

        DateBox.Date = DateTime.Today.AddDays(1);
        TimeBox.Time = new TimeSpan(9, 0, 0);

        UpdatePreview();
    }

    private DateTime BuildDateTime() => DateBox.Date.Date + TimeBox.Time;

    private void OnInputChanged()
    {
        if (ConfirmBox.IsChecked) ConfirmBox.IsChecked = false;
        UpdatePreview();
    }

    private void Date_Changed(object? sender, DateChangedEventArgs e) => OnInputChanged();

    private void Time_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == TimePicker.TimeProperty.PropertyName) OnInputChanged();
    }

    private void UpdatePreview()
    {
        DateTime dt = BuildDateTime();
        Preview.Text = Humanize.ReadBack(dt);
        PreviewCountdown.Text = Humanize.Countdown(dt, DateTime.Now);
    }

    private void Confirm_Changed(object? sender, CheckedChangedEventArgs e)
    {
        SaveBtn.IsEnabled = e.Value;
    }

    private async void Save_Clicked(object? sender, EventArgs e)
    {
        string title = (TitleBox.Text ?? "").Trim();
        if (title.Length == 0)
        {
            await DisplayAlert("Compass", "Give it a name first (e.g. “Calculus final exam”).", "OK");
            return;
        }

        int prep = PrepBox.SelectedIndex switch { 1 => 1, 2 => 2, 3 => 3, 4 => 5, 5 => 7, 6 => 14, _ => 0 };

        MobileStore.Instance.Data.Deadlines.Add(new Deadline
        {
            Title = title,
            Due = BuildDateTime(),
            Kind = KindBox.SelectedItem as string ?? "Deadline",
            Critical = CriticalBox.IsChecked,
            PrepDays = prep,
            WhereToFind = (WhereBox.Text ?? "").Trim(),
            Notes = (NotesBox.Text ?? "").Trim(),
        });
        MobileStore.Instance.Save();

        await Shell.Current.GoToAsync("..");
    }
}
