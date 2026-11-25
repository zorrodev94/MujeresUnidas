using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.User.Subclasses;
using BaseUser = global::MujeresUnidas.Comunidad.Models.User.Base.User;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Community login with responsive desktop and mobile composition.</summary>
    public sealed class LoginPage : ContentPage
    {
        #region Operation Data
        private readonly ApiClient _api;
        private readonly CommunityMember _member;
        private readonly Entry _email = Ui.Input("Correo electrónico");
        private readonly Entry _password = Ui.Input("Contraseña", true);
        private readonly Label _feedback = Ui.Text(string.Empty, 12);
        #endregion Operation Data

        #region Initialization
        public LoginPage(ApiClient api)
        {
            _api = api;
            _member = new CommunityMember(api);
            Title = "Acceso";
            BackgroundColor = Ui.Background;
            NavigationPage.SetHasNavigationBar(this, false);
            _email.Keyboard = Keyboard.Email;

            var brand = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
            brand.Children.Add(Ui.Logo(166));
            brand.Children.Add(Ui.Illustration(292));
            brand.Children.Add(Ui.Text("Comunidad, apoyo y oportunidades para mujeres en Ahome.", 13));
            var branding = Ui.Card(brand, 17);

            var form = new VerticalStackLayout { Spacing = 10, VerticalOptions = LayoutOptions.Center };
            var compactBrand = Ui.Logo(90);
            form.Children.Add(compactBrand);
            form.Children.Add(Ui.Heading("¡Bienvenida!", 26));
            form.Children.Add(Ui.Text("Ingresa a la comunidad de Mujeres Unidas.", 14));
            form.Children.Add(Ui.Text("Correo electrónico", 12));
            form.Children.Add(Ui.Field(_email));
            form.Children.Add(Ui.Text("Contraseña", 12));
            form.Children.Add(Ui.Field(_password));
            var login = Ui.Primary("Iniciar sesión");
            login.Clicked += Login;
            form.Children.Add(login);
            var create = Ui.Secondary("Crear cuenta");
            create.Clicked += async (_, _) => await Navigation.PushAsync(new RegisterPage(_api));
            form.Children.Add(create);
            form.Children.Add(_feedback);
            form.Children.Add(Ui.Notice("Tu cuenta quedará pendiente de activación por Gestión. La validación presencial se simula durante el hackathon."));
            var formCard = Ui.Card(form, 18);
            Content = Ui.Scroll(Ui.ResponsiveAuth(branding, formCard, this, compactBrand,
                desktop => form.Spacing = desktop ? 14 : 9));
        }
        #endregion Initialization

        #region Task
        private async void Login(object? sender, EventArgs args)
        {
            if (sender is not Button button) return;
            if (string.IsNullOrWhiteSpace(_email.Text) || string.IsNullOrWhiteSpace(_password.Text))
            {
                _feedback.Text = "Ingresa tu correo y contraseña.";
                return;
            }
            button.IsEnabled = false;
            try
            {
                var result = await _member.LoginAsync(new BaseUser.LoginData
                {
                    Email = _email.Text.Trim(), Password = _password.Text
                });
                if (!result.Success) { _feedback.Text = result.Message; return; }
                Application.Current!.Windows[0].Page = new NavigationPage(new HomePage(_api))
                {
                    BarBackgroundColor = Ui.Background, BarTextColor = Ui.Plum
                };
            }
            catch (Exception error)
            {
                System.Diagnostics.Debug.WriteLine(error);
                _feedback.Text = "La respuesta del servidor fue inesperada. Consulta la salida de depuración.";
            }
            finally { button.IsEnabled = true; }
        }
        #endregion Task
    }
}
