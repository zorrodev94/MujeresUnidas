using System.Text.RegularExpressions;
using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.User.Subclasses;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Register request with an explicit approval step and a responsive two-column form.</summary>
    [UsesData(typeof(CommunityMember.RegistrationData))]
    public sealed class RegisterPage : ContentPage
    {
        #region Operation Data
        private readonly ApiClient _api;
        private readonly CommunityMember _member;
        private readonly Entry _firstName = Ui.Input("Nombre(s)");
        private readonly Entry _lastNames = Ui.Input("Apellidos");
        private readonly Entry _curp = Ui.Input("CURP de prueba (18 caracteres)");
        private readonly Entry _email = Ui.Input("tu@correo.com");
        private readonly Entry _password = Ui.Input("Mínimo 10 caracteres", true);
        private readonly Entry _confirmPassword = Ui.Input("Repite tu contraseña", true);
        private readonly Label _feedback = AuthUi.Description("Utiliza exclusivamente datos ficticios de demostración.", 12);
        private readonly Label _strength = AuthUi.Description("Protege tu cuenta con una contraseña de al menos 10 caracteres.", 12);
        private readonly CheckBox _eligible = new() { Color = Ui.Violet, IsChecked = false };
        private readonly Grid _fields = new() { ColumnSpacing = 17, RowSpacing = 13 };
        private readonly View[] _fieldViews;
        private readonly Button _submit = AuthUi.MainButton("Enviar solicitud");
        #endregion Operation Data
        #region Initialization
        public RegisterPage(ApiClient api)
        {
            _api = api;
            _member = new CommunityMember(api);
            Title = "Crear cuenta";
            BackgroundColor = AuthUi.Canvas;
            NavigationPage.SetHasNavigationBar(this, false);
            _email.Keyboard = Keyboard.Email;
            _curp.MaxLength = 18;
            _curp.TextChanged += (_, _) =>
            {
                string normalized = _curp.Text?.ToUpperInvariant() ?? string.Empty;
                if (_curp.Text != normalized) _curp.Text = normalized;
            };
            _password.TextChanged += (_, _) => UpdateStrength();

            _fieldViews =
            [
                AuthUi.Field(_firstName, "Nombre(s) *", "mu_auth_user.png"),
                AuthUi.Field(_lastNames, "Apellidos *", "mu_auth_user.png"),
                AuthUi.Field(_email, "Correo electrónico *", "mu_auth_mail.png"),
                AuthUi.Field(_curp, "CURP ficticia de demostración *", "mu_auth_shield.png"),
                AuthUi.Field(_password, "Contraseña *", reveal: true),
                AuthUi.Field(_confirmPassword, "Confirmar contraseña *", reveal: true)
            ];
            foreach (var field in _fieldViews) _fields.Children.Add(field);
            SetupFields(false);

            var head = new VerticalStackLayout { Spacing = 7 };
            head.Children.Add(AuthUi.Title("Crear una cuenta", 28));
            head.Children.Add(AuthUi.Description("Si te identificas como mujer, solicita acceso a la comunidad. La verificación es académica.", 14));
            head.Children.Add(AuthUi.PrivacyNote("1. Solicitud   ›   2. Revisión de Gestión   ›   3. Activación",
                "Comunidad dirigida a mujeres; Gestión verificará la cuenta sin inferir género por nombre o apariencia."));

            var form = new VerticalStackLayout { Spacing = 16 };
            var mobileHero = new Image
            {
                Source = "mu_r31_people.png", HeightRequest = 128,
                Aspect = Aspect.AspectFit, HorizontalOptions = LayoutOptions.Fill
            };
            form.Children.Add(mobileHero);
            form.Children.Add(head);
            form.Children.Add(_fields);
            form.Children.Add(_strength);
            var eligibility=new Grid { ColumnSpacing=10,ColumnDefinitions={new ColumnDefinition(new GridLength(42)),
                new ColumnDefinition(GridLength.Star)} };
            eligibility.Add(_eligible,0,0);
            eligibility.Add(new Label
            {
                Text="Declaro que esta solicitud es para una mujer y acepto la revisión institucional de demostración.",
                TextColor=Ui.Plum,FontSize=12,VerticalTextAlignment=TextAlignment.Center,
                LineBreakMode=LineBreakMode.WordWrap,HorizontalOptions=LayoutOptions.Fill
            },1,0);
            form.Children.Add(eligibility);
            form.Children.Add(AuthUi.PrivacyNote("Tu privacidad es importante",
                "La CURP es ficticia para este prototipo. No utilices información personal real."));
            form.Children.Add(_feedback);
            _submit.Clicked += Register;
            form.Children.Add(_submit);
            var back = AuthUi.OutlineButton("Volver a iniciar sesión");
            back.Clicked += async (_, _) => await Navigation.PopAsync();
            form.Children.Add(back);

            var branding = AuthUi.ArtPanel(includeActions: false);
            var right = AuthUi.Surface(form, 28);
            var responsive = AuthUi.Responsive(this, branding, right);
            SizeChanged += (_, _) =>
            {
                mobileHero.IsVisible = Width < 900;
                SetupFields(Width >= 900);
            };

            var root = new Grid
            {
                RowSpacing = 0,
                RowDefinitions =
                {
                    new RowDefinition(new GridLength(60)),
                    new RowDefinition(GridLength.Star)
                }
            };
            root.Add(AuthUi.TitleBar("Registro y solicitud de acceso"), 0, 0);
            root.Add(new ScrollView
            {
                Content = responsive, BackgroundColor = AuthUi.Canvas,
                VerticalScrollBarVisibility = ScrollBarVisibility.Default
            }, 0, 1);
            Content = root;
        }
        private void SetupFields(bool desktop)
        {
            _fields.ColumnDefinitions.Clear();
            _fields.RowDefinitions.Clear();
            int columns = desktop ? 2 : 1;
            for (int col = 0; col < columns; col++)
                _fields.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            for (int row = 0; row < (_fieldViews.Length + columns - 1) / columns; row++)
                _fields.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (int i = 0; i < _fieldViews.Length; i++)
            {
                Grid.SetColumn(_fieldViews[i], i % columns);
                Grid.SetRow(_fieldViews[i], i / columns);
            }
        }
        private void UpdateStrength()
        {
            var password = _password.Text ?? string.Empty;
            bool hasNumbers = password.Any(char.IsDigit);
            bool hasLetters = password.Any(char.IsLetter);
            bool hasSymbols = password.Any(ch => !char.IsLetterOrDigit(ch));
            _strength.Text = password.Length < 10
                ? "Contraseña: mínimo 10 caracteres."
                : (hasNumbers && hasLetters && hasSymbols)
                    ? "Contraseña con letras, números y símbolos."
                    : "Sugerencia: combina letras, números y algún símbolo.";
        }
        #endregion Initialization
        #region Task
        private async void Register(object? sender, EventArgs args)
        {
            string displayName = $"{_firstName.Text?.Trim()} {_lastNames.Text?.Trim()}".Trim();
            string curp = (_curp.Text ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(_firstName.Text) || string.IsNullOrWhiteSpace(_lastNames.Text)
                || displayName.Length < 2 || displayName.Length > 100)
            { _feedback.Text = "Escribe nombre y apellidos (máximo 100 caracteres)."; return; }
            if (!Regex.IsMatch(curp, "^[A-Z0-9]{18}$"))
            { _feedback.Text = "La CURP ficticia debe contener exactamente 18 caracteres alfanuméricos."; return; }
            if (string.IsNullOrWhiteSpace(_email.Text) || !System.Net.Mail.MailAddress.TryCreate(_email.Text.Trim(), out var address)
                || address.Address != _email.Text.Trim())
            { _feedback.Text = "Escribe un correo electrónico válido."; return; }
            if ((_password.Text ?? string.Empty).Length < 10 || _password.Text!.Length > 128)
            { _feedback.Text = "La contraseña debe tener entre 10 y 128 caracteres."; return; }
            if (!_eligible.IsChecked)
            { _feedback.Text="Confirma que cumples las condiciones de participación en Comunidad.";return; }
            if (_password.Text != _confirmPassword.Text)
            { _feedback.Text = "Las contraseñas no coinciden."; return; }
            _submit.IsEnabled = false;
            _feedback.Text = "Enviando solicitud...";
            try
            {
                var response = await _member.RegisterAsync(new CommunityMember.RegistrationData
                {
                    DisplayName = displayName,
                    Curp = curp,
                    Email = _email.Text!.Trim(), Password = _password.Text!, EligibilityAffirmed=_eligible.IsChecked
                });
                _feedback.Text = response.Message;
                if (response.Success)
                {
                    await DisplayAlertAsync("Solicitud recibida",
                        response.Message+"\n\nNo hay plazo de caducidad. Las sedes de Ahome son ficticias; no acudas a una dirección simulada.", "Entendido");
                    await Navigation.PopAsync();
                }
            }
            catch (Exception error)
            {
                System.Diagnostics.Debug.WriteLine(error);
                _feedback.Text = "No se pudo enviar la solicitud. Revisa la conexión con el servidor.";
            }
            finally { _submit.IsEnabled = true; }
        }
        #endregion Task
    }
}
