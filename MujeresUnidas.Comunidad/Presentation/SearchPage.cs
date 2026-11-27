using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.Publications;
using MujeresUnidas.Comunidad.Models.User.Profiles;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>
    /// Dedicated discovery destination. Profile search and publication discovery are independent
    /// of the conversations inbox. Private account fields are never shown.
    /// </summary>
    public sealed class SearchPage : ContentPage
    {
        #region Operation Data
        private readonly ApiClient _api;
        private readonly CommunityDirectory _directory;
        private readonly Post _posts;
        private readonly Entry _term = Ui.Input("Busca personas, momentos, blogs o recursos...");
        private readonly VerticalStackLayout _results = new() { Spacing = 12 };
        private readonly Label _feedback = Ui.Text(string.Empty, 12);
        private readonly Button _people = Ui.Primary("Personas");
        private readonly Button _postsButton = Ui.Secondary("Publicaciones");
        private bool _showPeople = true;
        private bool _loading;
        #endregion Operation Data

        #region Initialization
        public SearchPage(ApiClient api)
        {
            _api = api; _directory = new CommunityDirectory(api); _posts = new Post(api);
            Title = "Buscar";
            NavigationPage.SetHasNavigationBar(this, false);
            var root = new Grid { BackgroundColor = Ui.Background, RowDefinitions =
            {
                new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)
            }};
            var body = new VerticalStackLayout { Spacing = 13, Padding = new Thickness(16, 12, 16, 24),
                MaximumWidthRequest = 900, HorizontalOptions = LayoutOptions.Fill };
            body.Children.Add(Ui.Header(this, api, "Descubre la comunidad"));
            body.Children.Add(Ui.Heading("Descubrir", 27));
            body.Children.Add(Ui.Text("Encuentra integrantes, publicaciones y recursos públicos; los datos de registro son privados.", 13));
            body.Children.Add(Ui.Card(new Image { Source = "mu_header.png", HeightRequest = 106, Aspect = Aspect.AspectFill }, 0));
            body.Children.Add(Ui.Field(_term));
            var search = Ui.Primary("Buscar ahora");
            search.Clicked += async (_,_) => await LoadAsync();
            _term.Completed += async (_,_) => await LoadAsync();
            body.Children.Add(search);
            var choices = new Grid { ColumnSpacing = 8, ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)
            }};
            _people.Clicked += async (_,_) => { _showPeople = true; UpdateChoices(); await LoadAsync(); };
            _postsButton.Clicked += async (_,_) => { _showPeople = false; UpdateChoices(); await LoadAsync(); };
            choices.Add(_people, 0, 0); choices.Add(_postsButton, 1, 0);
            body.Children.Add(choices);
            body.Children.Add(_feedback);
            body.Children.Add(_results);
            root.Add(Ui.Scroll(body), 0, 0);
            root.Add(Ui.MainTabs(this, api, "Buscar"), 0, 1);
            Content = root;
        }
        #endregion Initialization

        #region Task
        protected override async void OnAppearing()
        {
            base.OnAppearing(); await LoadAsync();
        }
        private void UpdateChoices()
        {
            _people.BackgroundColor = _showPeople ? Ui.Violet : Ui.Pink;
            _people.TextColor = _showPeople ? Colors.White : Ui.Plum;
            _postsButton.BackgroundColor = _showPeople ? Ui.Pink : Ui.Violet;
            _postsButton.TextColor = _showPeople ? Ui.Plum : Colors.White;
        }
        private async Task LoadAsync()
        {
            if (_loading) return;
            _loading = true;
            _results.Children.Clear();
            _feedback.Text = "Buscando...";
            try
            {
                string query = (_term.Text ?? string.Empty).Trim();
                if (_showPeople)
                {
                    var people = await _directory.SearchAsync(query);
                    if (!string.IsNullOrWhiteSpace(people.Error)) { _feedback.Text = people.Error; return; }
                    _feedback.Text = $"{people.Items.Count} perfiles encontrados";
                    foreach (var person in people.Items)
                    {
                        var face = Ui.Avatar(46);
                        var details = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
                        details.Children.Add(Ui.Heading(person.DisplayName, 16));
                        details.Children.Add(Ui.Text(person.CommunityRole == "Staff" ? "Staff de Comunidad" : "Miembro de Comunidad", 11));
                        var row = new Grid { ColumnSpacing = 10, ColumnDefinitions =
                        {
                            new ColumnDefinition(new GridLength(52)),new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(new GridLength(82))
                        }};
                        row.Add(face,0,0); row.Add(details,1,0);
                        var view = Ui.Secondary("Ver perfil"); view.FontSize = 11;
                        var id = person.UserId;
                        view.Clicked += async (_,_) => await Navigation.PushAsync(new PublicProfilePage(_api,id));
                        row.Add(view,2,0);
                        _results.Children.Add(Ui.Card(row,10));
                        var image = await _directory.ImageAsync(id,"avatar");
                        if(image is not null)face.Source = Ui.BytesSource(image);
                    }
                    if (people.Items.Count==0) _results.Children.Add(Ui.Notice("No encontramos personas con esos datos."));
                }
                else
                {
                    var posts = await _posts.FeedAsync(null);
                    if (!posts.Success) { _feedback.Text = posts.Error; return; }
                    var matches = posts.Items.Where(p => query.Length == 0 ||
                        p.Title.Contains(query,StringComparison.OrdinalIgnoreCase) ||
                        p.Summary.Contains(query,StringComparison.OrdinalIgnoreCase) ||
                        p.Author.Contains(query,StringComparison.OrdinalIgnoreCase)).ToList();
                    _feedback.Text = $"{matches.Count} publicaciones disponibles";
                    foreach (var post in matches)
                    {
                        var item = new VerticalStackLayout { Spacing = 7 };
                        item.Children.Add(Ui.Tag(PostDetailPage.CategoryName(post.Category)));
                        item.Children.Add(Ui.Heading(post.Title,17));
                        item.Children.Add(Ui.Text(post.Author + " · " + post.CreatedUtc.ToLocalTime().ToString("d"),11));
                        item.Children.Add(Ui.Text(post.Summary,12));
                        var open = Ui.Secondary("Leer publicación");
                        open.Clicked += async (_,_) => await Navigation.PushAsync(new PostDetailPage(post,_api));
                        item.Children.Add(open);
                        _results.Children.Add(Ui.Card(item,14));
                    }
                    if(matches.Count==0) _results.Children.Add(Ui.Notice("No hay publicaciones que coincidan con tu búsqueda."));
                }
            }
            finally { _loading=false; }
        }
        #endregion Task
    }
}
