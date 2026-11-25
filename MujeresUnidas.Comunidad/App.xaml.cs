using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Presentation;
namespace MujeresUnidas.Comunidad
{
public partial class App : Application
{
    private readonly ApiClient _api=new();
    public App()=>InitializeComponent();
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new NavigationPage(new LoginPage(_api))
        {
            BarBackgroundColor = Ui.Background,
            BarTextColor = Ui.Plum
        });
#if WINDOWS
        // Windows Machine previews the phone layout rather than stretching across the desktop.
        window.Width = 430;
        window.Height = 700;
        window.MinimumWidth = 380;
        window.MaximumWidth = 470;
        window.MinimumHeight = 620;
        window.MaximumHeight = 740;
#endif
        return window;
    }
}
}
