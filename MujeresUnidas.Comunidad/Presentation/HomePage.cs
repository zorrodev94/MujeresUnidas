using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.Publications;
using MujeresUnidas.Comunidad.Models.User.Subclasses;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Home has three featured entries, followed by dedicated Blog, Guide, Job and Event views.</summary>
    public sealed class HomePage : ContentPage
    {
        #region Operation Data
        private readonly ApiClient _api;
        private readonly Post _posts;
        private readonly CommunityMember _member;
        private readonly VerticalStackLayout _cards = new() { Spacing=12 };
        private readonly Label _heading = Ui.Heading("Publicaciones de la comunidad",24);
        private readonly SearchBar _search=new(){Placeholder="Buscar publicaciones...",TextColor=Ui.Plum,PlaceholderColor=Ui.Muted,BackgroundColor=Colors.White};
        private IReadOnlyList<Post.ViewData> _feed=Array.Empty<Post.ViewData>();
        private readonly Label _feedback = Ui.Text(string.Empty,12);
        private string? _category;

        private readonly Dictionary<string,Button> _buttons = new();
        #endregion Operation Data
        #region Initialization
        public HomePage(ApiClient api)
        {
            _api=api;
            _posts=new Post(api);
            _member=new CommunityMember(api);
            Title="Inicio";
            NavigationPage.SetHasNavigationBar(this,false);
            var root=new Grid {BackgroundColor=Ui.Background,
                RowDefinitions={new RowDefinition(GridLength.Star),new RowDefinition(GridLength.Auto)}};
            var body=new VerticalStackLayout {Spacing=13,Padding=new Thickness(15,12,15,30)};
            body.Children.Add(Ui.Header(this,api,"Comunidad · Inicio"));
            var welcome=new VerticalStackLayout{Spacing=6};
            welcome.Children.Add(Ui.Heading("Una comunidad que acompaña y transforma",20));
            welcome.Children.Add(Ui.Text("Comparte experiencias, consulta recursos y encuentra actividades aprobadas.",12));
            welcome.Children.Add(new Image{Source="mu_hero_welcome.png",HeightRequest=190,Aspect=Aspect.AspectFit});
            body.Children.Add(Ui.Card(welcome,12));
            var openEditor=Ui.Primary("＋ Crear Blog o publicación");
            openEditor.Clicked+=async (_,_)=>await Navigation.PushAsync(new CreatePostPage(_api));
            body.Children.Add(openEditor);
            var surveyBanner=new VerticalStackLayout{Spacing=7};
            surveyBanner.Children.Add(Ui.Tag("ENCUESTA VOLUNTARIA · IMPACTO SOCIAL",true));
            surveyBanner.Children.Add(Ui.Heading("¿Estudias o trabajas?",19));
            surveyBanner.Children.Add(Ui.Text("Ayúdanos a medir cambios y oportunidades. Tu respuesta individual será privada.",12));
            var openSurvey=Ui.Primary("Responder encuesta de progreso");
            openSurvey.Clicked+=async (_,_)=>await Navigation.PushAsync(new CommunitySurveyPage(_api));
            surveyBanner.Children.Add(openSurvey);
            body.Children.Add(Ui.Card(surveyBanner,15));
            body.Children.Add(_heading);
            _search.TextChanged+=(_,_)=>RenderFiltered();
            // SearchBar is an actual native input. It filters the already loaded feed without API calls per keystroke.
            body.Children.Add(_search);
            var filters=new HorizontalStackLayout {Spacing=7};
            AddFilter(filters,"Todas",string.Empty);
            AddFilter(filters,"Blogs","Blog");
            AddFilter(filters,"Guías","Guide");
            AddFilter(filters,"Empleos","Job");
            AddFilter(filters,"Eventos","Event");
            body.Children.Add(new ScrollView {Orientation=ScrollOrientation.Horizontal,Content=filters,
                HorizontalScrollBarVisibility=ScrollBarVisibility.Never});
            body.Children.Add(_feedback);
            body.Children.Add(_cards);
            body.Children.Add(Ui.Notice("Si estás en peligro inmediato, llama al 911. Esta aplicación académica no envía alertas ni reemplaza atención profesional."));
            root.Add(Ui.Scroll(body),0,0);
            root.Add(Ui.MainTabs(this,api,"Inicio"),0,1);
            Content=root;
        }

        private void AddFilter(HorizontalStackLayout layout,string title,string category)
        {
            var control=Ui.Secondary(title);
            control.FontSize=12;control.HeightRequest=43;control.Padding=new Thickness(11,0);
            control.Clicked+=async (_,_)=>await SelectAsync(category.Length==0?null:category);
            layout.Children.Add(control);
            _buttons["filter:"+category]=control;
        }

        #endregion Initialization
        #region Task
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await SelectAsync(_category);
        }

        private async Task SelectAsync(string? category)
        {
            _category=category;
            _heading.Text=category switch
            {
                "Blog"=>"Blogs", "Guide"=>"Guías", "Job"=>"Empleos", "Event"=>"Eventos", _=>"Publicaciones de la comunidad"
            };
            foreach(var pair in _buttons)
            {
                bool selected=pair.Key=="filter:"+(category??string.Empty);
                pair.Value.BackgroundColor=selected ? Ui.Lavender : Colors.White;
            }
            _feedback.Text="Cargando...";
            _cards.Children.Clear();
            var response=await _posts.FeedAsync(category,false);
            if(category!=_category)return;
            if(!response.Success)
            {
                _feedback.Text=response.Error;
                return;
            }
            _feedback.Text=string.Empty;
            _feed=response.Items;
            RenderFiltered();
        }

        private void RenderFiltered()
        {
            _cards.Children.Clear();
            var query=(_search.Text??string.Empty).Trim();
            var visible=_feed.Where(p=>p.Category!="Moment" && (query.Length==0 ||
                p.Title.Contains(query,StringComparison.OrdinalIgnoreCase)||
                p.Summary.Contains(query,StringComparison.OrdinalIgnoreCase)||
                p.Author.Contains(query,StringComparison.OrdinalIgnoreCase)||
                (p.Subcategory?.Contains(query,StringComparison.OrdinalIgnoreCase)??false))).ToList();
            if(visible.Count==0)_cards.Children.Add(Ui.Notice("No hay publicaciones aprobadas que coincidan con tu búsqueda."));
            foreach(var item in visible)_cards.Children.Add(RenderCard(item));
        }
        /// <summary>Community publication card: content-first feed with rounded cover and clear author metadata.</summary>
        private Border RenderCard(Post.ViewData post)
        {
            string illustration=post.Category switch
            {
                "Guide"=>"mu_demo_post_guide.jpg", "Event"=>"mu_demo_post_event.jpg",
                "Job"=>"mu_demo_post_job.jpg",_=>"mu_demo_post_support.jpg"
            };
            var content=new VerticalStackLayout{Spacing=10};
            var identity=new HorizontalStackLayout{Spacing=8};
            identity.Children.Add(Ui.Icon("nav_profile.png",30));
            var author=new VerticalStackLayout{Spacing=2,VerticalOptions=LayoutOptions.Center};
            author.Children.Add(Ui.Heading(post.Author,14));
            author.Children.Add(Ui.Text(post.CreatedUtc.ToLocalTime().ToString("dd MMM yyyy"),11));
            identity.Children.Add(author);content.Children.Add(identity);
            var cover=new Image{Source=illustration,HeightRequest=180,
                Aspect=Aspect.AspectFill,HorizontalOptions=LayoutOptions.Fill};
            content.Children.Add(Ui.Cover(cover,180));
            _=LoadCoverAsync(post.Id,cover);
            var tags=new HorizontalStackLayout{Spacing=6};
            tags.Children.Add(Ui.Tag(PostDetailPage.CategoryName(post.Category)));
            if(post.Featured)tags.Children.Add(Ui.Tag("Destacada",true));
            content.Children.Add(tags);
            content.Children.Add(Ui.Heading(post.Title,18));
            content.Children.Add(Ui.Text(post.Summary,13));
            content.Children.Add(Ui.Text("Abrir publicación y participar",11));
            var card=Ui.Card(content,14);
            var tap=new TapGestureRecognizer();
            tap.Tapped+=async (_,_)=>await Navigation.PushAsync(new PostDetailPage(post,_api));
            card.GestureRecognizers.Add(tap);
            return card;
        }
        private async Task LoadCoverAsync(Guid id,Image target)
        {
            var bytes=await _posts.CoverAsync(id);
            if(bytes is not null)target.Source=Ui.BytesSource(bytes);
        }
        #endregion Task
    }
}
