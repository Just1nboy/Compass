using Compass.Mobile.Pages;

namespace Compass.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("add", typeof(AddDeadlinePage));
    }
}
