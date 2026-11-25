using MujeresUnidas.Gestion.Connection;
using MujeresUnidas.Gestion.Presentation;

namespace MujeresUnidas.Gestion
{
internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new AccessForm(new ApiConnection()));
    }
}
}
