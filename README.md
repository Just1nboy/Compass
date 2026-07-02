# Compass

A personal deadline and executive-function assistant that keeps important dates from slipping through the cracks. It runs quietly in the background, escalates reminders as a deadline gets closer, and syncs everything between your PC and phone automatically.

## Projects

- **Compass.Core** shared models and the two-way cloud sync engine (Supabase over REST, per-item merge).
- **Compass** Windows desktop app (WPF, .NET 9). Lives in the tray, fires reminders, imports deadlines from email, and includes an AI assistant.
- **Compass.Mobile** Android app (.NET MAUI) with the same deadlines, playbooks, and inbox, plus a home screen quick-capture widget.

## Key features

- **Read-back confirmation** on every deadline, so a mistyped date or time is caught before it costs you.
- **Playbooks**, reusable step-by-step checklists for things like applications and course registration.
- **Capture inbox** for dumping any date or task the instant you hear it.
- **Escalating reminders** from a week out down to the day itself, with re-nudges for critical items.
- **Instant two-way sync**, so a change on one device shows up on the other almost immediately.
- **Email import** (desktop) that scans for deadlines and action items over IMAP.

## Building

```
# Desktop
dotnet build Compass/Compass.csproj -c Release

# Android (needs the Android SDK and maui-android workload)
dotnet publish Compass.Mobile/Compass.Mobile.csproj -f net9.0-android -c Release -p:AndroidPackageFormat=apk
```
