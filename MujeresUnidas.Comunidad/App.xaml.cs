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
        // Preview the real phone layout on Windows. The UI expands adaptively on tablets.
        window.Width = 410;
        window.Height = 820;
        window.MinimumWidth = 350;
        window.MinimumHeight = 620;
#endif
        // Bank-style client lock: sensitive content cannot remain visible after the app backgrounds.
        window.Stopped+=async (_,_)=>
        {
            if(string.IsNullOrEmpty(_api.Token))return;
            // Replace the visible page immediately; even if remote revocation cannot complete.
            window.Page=new NavigationPage(new LoginPage(_api))
            {
                BarBackgroundColor=Ui.Background,BarTextColor=Ui.Plum
            };
            await _api.SignOutAsync();
        };
        return window;
    }
}
}
