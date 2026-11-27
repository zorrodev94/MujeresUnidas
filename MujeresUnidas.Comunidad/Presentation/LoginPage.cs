using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.User.Subclasses;
using BaseUser = global::MujeresUnidas.Comunidad.Models.User.Base.User;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Accessible Community login, adapting the approved design to Windows and Android.</summary>
    [UsesData(typeof(BaseUser.LoginData))]
    public sealed class LoginPage : ContentPage
    {
        #region Operation Data
        private readonly ApiClient _api;
        private readonly CommunityMember _member;
        private readonly Entry _email = Ui.Input("tu@correo.com");
        private readonly Entry _password = Ui.Input("Ingresa tu contraseña", true);
        private readonly Label _feedback = AuthUi.Description(string.Empty, 12);
        private readonly Button _loginButton;
        #endregion Operation Data
        #region Initialization
        public LoginPage(ApiClient api)
        {
            _api = api;
            _member = new CommunityMember(api);
            Title = "Iniciar sesión";
            BackgroundColor = AuthUi.Canvas;
            NavigationPage.SetHasNavigationBar(this, false);
            _email.Keyboard = Keyboard.Email;
            _email.ReturnType = ReturnType.Next;
            _password.ReturnType = ReturnType.Go;
            _password.Completed += (_, _) => Login(_loginButton, EventArgs.Empty);

            var form = new VerticalStackLayout { Spacing = 16 };
            var mobileBrand = new Image
            {
                Source = "mu_brand.png", HeightRequest = 102,
                HorizontalOptions = LayoutOptions.Fill, Aspect = Aspect.AspectFit
            };
            var mobileArt = new Image
            {
                Source = "mu_r31_people.png", HeightRequest = 125,
                HorizontalOptions = LayoutOptions.Fill, Aspect = Aspect.AspectFit
            };
            form.Children.Add(mobileBrand);
            form.Children.Add(mobileArt);
            form.Children.Add(AuthUi.Title("Iniciar sesión", 28));
            form.Children.Add(AuthUi.Description("Accede a tu cuenta y forma parte de la comunidad.", 14));
            form.Children.Add(AuthUi.Field(_email, "Correo electrónico", "mu_auth_mail.png"));
            form.Children.Add(AuthUi.Field(_password, "Contraseña", "mu_auth_lock.png", reveal: true));

            var options = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };
            options.Add(AuthUi.Description("Sesión protegida por el servidor", 11), 0, 0);
            var help = new Button
            {
                Text = "¿Problemas para acceder?", FontSize = 11,
                TextColor = AuthUi.Purple, BackgroundColor = Colors.Transparent,
                Padding = new Thickness(2), HeightRequest = 32
            };
            help.Clicked += async (_, _) => await DisplayAlertAsync("Ayuda para acceder",
                "Comunícate con el personal responsable de Gestión para revisar tu cuenta. La recuperación de contraseña en línea aún no está habilitada en este prototipo.", "Entendido");
            options.Add(help, 1, 0);
            form.Children.Add(options);

            _loginButton = AuthUi.MainButton("Entrar a la Comunidad");
            _loginButton.Clicked += Login;
            form.Children.Add(_loginButton);
            form.Children.Add(_feedback);
            var divider = new Label
            {
                Text = "¿Aún no tienes una cuenta?", FontSize = 12,
                TextColor = Ui.Muted, HorizontalTextAlignment = TextAlignment.Center
            };
            form.Children.Add(divider);
            var create = AuthUi.OutlineButton("Crear una cuenta");
            create.Clicked += async (_, _) => await Navigation.PushAsync(new RegisterPage(_api));
            form.Children.Add(create);
            form.Children.Add(AuthUi.PrivacyNote("Comunidad con acceso autorizado",
                "Las cuentas nuevas permanecen pendientes hasta su revisión por Gestión."));

            var right = AuthUi.Surface(form, 28);
            var art = AuthUi.ArtPanel(includeActions: true);
            var adaptive = AuthUi.Responsive(this, art, right);
            SizeChanged += (_, _) =>
            {
                mobileBrand.IsVisible = Width < 900;
                mobileArt.IsVisible = Width < 900;
            };

            Content = new Grid
            {
                RowSpacing = 0, RowDefinitions =
                {
                    new RowDefinition(new GridLength(60)),
                    new RowDefinition(GridLength.Star)
                },
                Children =
                {
                    AuthUi.TitleBar("Acceso seguro"),
                }
            };
            ((Grid)Content).Add(new ScrollView
            {
                Content = adaptive, BackgroundColor = AuthUi.Canvas,
                VerticalScrollBarVisibility = ScrollBarVisibility.Default
            }, 0, 1);
        }
        #endregion Initialization
        #region Task
        private async void Login(object? sender, EventArgs args)
        {
            if (string.IsNullOrWhiteSpace(_email.Text) || string.IsNullOrWhiteSpace(_password.Text))
            {
                _feedback.Text = "Ingresa tu correo y contraseña.";
                return;
            }
            _loginButton.IsEnabled = false;
            _feedback.Text = "Comprobando credenciales...";
            try
            {
                var result = await _member.LoginAsync(new BaseUser.LoginData
                {
                    Email = _email.Text.Trim(), Password = _password.Text
                });
                if (!result.Success)
                {
                    _feedback.Text = result.Message;
                    return;
                }
                Application.Current!.Windows[0].Page = new NavigationPage(new HomePage(_api))
                {
                    BarBackgroundColor = Ui.Background, BarTextColor = Ui.Plum
                };
            }
            catch (Exception error)
            {
                System.Diagnostics.Debug.WriteLine(error);
                _feedback.Text = "No fue posible iniciar sesión. Revisa el estado del servidor.";
            }
            finally { _loginButton.IsEnabled = true; }
        }
        #endregion Task
    }
}
