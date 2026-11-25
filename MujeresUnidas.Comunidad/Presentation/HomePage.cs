using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.User.Subclasses;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Community home shell with editorial preview cards and reserved, clearly labeled future sections.</summary>
    public sealed class HomePage : ContentPage
    {
        #region Operation Data
        private readonly ApiClient _api;
        private readonly CommunityMember _member;
        private readonly Label _status = Ui.Text(string.Empty, 12);
        #endregion Operation Data

        #region Initialization
        public HomePage(ApiClient api)
        {
            _api = api;
            _member = new CommunityMember(api);
            Title = "Inicio";
            BackgroundColor = Ui.Background;
            NavigationPage.SetHasNavigationBar(this, false);

            var shell = new Grid
            {
                BackgroundColor = Ui.Background,
                RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) }
            };
            var body = new VerticalStackLayout
            {
                Padding = new Thickness(17, 12, 17, 25), Spacing = 17,
                MaximumWidthRequest = 1040, HorizontalOptions = LayoutOptions.Fill
            };

            var top = new Grid
            {
                HeightRequest = 153,
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(58)) }
            };
            var landscape = new Image { Source = "mu_header.png", Aspect = Aspect.AspectFill, Opacity = 0.88 };
            top.Add(landscape, 0, 0);
            Grid.SetColumnSpan(landscape, 2);
            var mark = Ui.Logo(145);
            top.Add(mark, 0, 0);
            var notices = Ui.Secondary("♧");
            notices.HeightRequest = 46;
            notices.WidthRequest = 51;
            notices.VerticalOptions = LayoutOptions.Start;
            notices.Clicked += async (_, _) => await DisplayAlert("Notificaciones", "Disponibles en una próxima revisión.", "Aceptar");
            top.Add(notices, 1, 0);
            body.Children.Add(top);

            var search = Ui.Input("Buscar en la comunidad · próximamente");
            search.IsEnabled = false;
            body.Children.Add(Ui.Field(search));

            var intro = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(new GridLength(1.15, GridUnitType.Star)),
                    new ColumnDefinition(new GridLength(1, GridUnitType.Star)) },
                ColumnSpacing = 5, MinimumHeightRequest = 205
            };
            var welcomeText = new VerticalStackLayout
            {
                Spacing = 12, Padding = new Thickness(4, 12, 0, 0),
                VerticalOptions = LayoutOptions.Center
            };
            welcomeText.Children.Add(Ui.Heading("¡Bienvenida a Mujeres Unidas!", 22));
            welcomeText.Children.Add(Ui.Text("Un espacio para compartir, aprender y crecer juntas en Ahome.", 12));
            intro.Add(welcomeText, 0, 0);
            intro.Add(Ui.Illustration(210), 1, 0);
            body.Children.Add(Ui.Card(intro, 11));

            var categories = new HorizontalStackLayout { Spacing = 9, Padding = new Thickness(2, 4) };
            string[] labels = { "Publicaciones", "Eventos", "Comunidad", "Guías", "Libros", "Empleos" };
            foreach (string category in labels)
            {
                var button = Ui.Secondary(category);
                button.WidthRequest = category == "Publicaciones" ? 145 : 113;
                button.FontSize = 12;
                button.BackgroundColor = category == "Publicaciones" ? Ui.Lavender : Colors.White;
                button.Clicked += async (_, _) => await DisplayAlert(category,
                    "Esta sección se habilitará en una revisión futura. Las tarjetas actuales son ejemplos de diseño.", "Entendido");
                categories.Children.Add(button);
            }
            body.Children.Add(new ScrollView
            {
                Orientation = ScrollOrientation.Horizontal, Content = categories,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Never
            });

            var heading = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
            };
            heading.Add(Ui.Heading("Publicaciones destacadas", 22), 0, 0);
            var more = Ui.Secondary("Ver todas ›");
            more.HeightRequest = 40;
            more.Clicked += async (_, _) => await DisplayAlert("Publicaciones", "La moderación y los destacados estarán disponibles después de R1.", "Aceptar");
            heading.Add(more, 1, 0);
            body.Children.Add(heading);
            body.Children.Add(Ui.Text("Ejemplos visuales para mostrar cómo se presentarán los contenidos autorizados.", 11));

            var previews = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
                RowSpacing = 12, ColumnSpacing = 12
            };
            var samples = new[]
            {
                ("Bienestar emocional", "mu_story.png", "Publicación · demostración"),
                ("Taller de autocuidado", "mu_event.png", "Evento · próximamente"),
                ("Programas de apoyo", "mu_guide.png", "Guía · próximamente"),
                ("Lecturas que inspiran", "mu_books.png", "Libros · próximamente")
            };
            for (int i = 0; i < samples.Length; i++)
            {
                var item = samples[i];
                var content = new VerticalStackLayout { Spacing = 7 };
                content.Children.Add(new Image { Source = item.Item2, HeightRequest = 105, Aspect = Aspect.AspectFill });
                content.Children.Add(Ui.Heading(item.Item1, 15));
                content.Children.Add(Ui.Text(item.Item3, 11));
                var card = Ui.Card(content, 10);
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (_, _) => await DisplayAlert(item.Item1,
                    "Tarjeta ilustrativa: no hay publicaciones reales en esta versión.", "Aceptar");
                card.GestureRecognizers.Add(tap);
                previews.Add(card, i % 2, i / 2);
            }
            body.Children.Add(previews);
            body.Children.Add(Ui.Notice("Ayuda y orientación: si necesitas ayuda inmediata, utiliza el 911. Esta versión no recibe alertas ni ofrece atención institucional."));
            body.Children.Add(_status);
            var center = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star) } };
            center.Add(body, 0, 0);
            shell.Add(Ui.Scroll(center), 0, 0);

            var tabs = new Grid
            {
                BackgroundColor = Colors.White, HeightRequest = 76,
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }
            };
            var home = Ui.Secondary("⌂  Inicio");
            home.BackgroundColor = Ui.Lavender;
            var messages = Ui.Secondary("✉  Mensajes");
            messages.BackgroundColor = Colors.White;
            var profile = Ui.Secondary("♙  Perfil");
            profile.BackgroundColor = Colors.White;
            messages.Clicked += async (_, _) => await DisplayAlert("Mensajes", "La mensajería estará disponible en otra revisión.", "Entendido");
            profile.Clicked += async (_, _) => await Navigation.PushAsync(new ProfilePage(_api));
            tabs.Add(home, 0, 0);
            tabs.Add(messages, 1, 0);
            tabs.Add(profile, 2, 0);
            shell.Add(tabs, 0, 1);
            Content = shell;
        }
        #endregion Initialization

        #region Task
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            var result = await _member.GetProfileAsync();
            _status.Text = result.Success ? string.Empty :
                "No fue posible comprobar la sesión. Revisa tu conexión o inicia sesión nuevamente.";
        }
        #endregion Task
    }
}
