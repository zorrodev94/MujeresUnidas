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
        private readonly Label _heading = Ui.Heading("Destacados",25);
        private readonly Label _feedback = Ui.Text(string.Empty,12);
        private string? _category;
        private bool _resetOnProfileReturn;
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
            body.Children.Add(Ui.Logo(78));
            body.Children.Add(Ui.Text("Comunidad · recursos, experiencias y oportunidades",12));
            body.Children.Add(_heading);
            var filters=new HorizontalStackLayout {Spacing=7};
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
            var bottom=new Grid {BackgroundColor=Colors.White,HeightRequest=66,
                ColumnDefinitions={new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Star)}};
            AddTab(bottom,"Inicio",0,null);
            AddTab(bottom,"Blogs",1,"Blog");
            AddTab(bottom,"Guías",2,"Guide");
            AddTab(bottom,"Empleos",3,"Job");
            AddTab(bottom,"Eventos",4,"Event");
            var profile=Tab("Perfil");
            profile.Clicked+=async (_,_)=>
            {
                _resetOnProfileReturn=true;
                await Navigation.PushAsync(new ProfilePage(_api));
            };
            bottom.Add(profile,5,0);
            root.Add(bottom,0,1);
            Content=root;
        }

        private void AddFilter(HorizontalStackLayout layout,string title,string category)
        {
            var control=Ui.Secondary(title);
            control.FontSize=12;control.HeightRequest=43;control.Padding=new Thickness(11,0);
            control.Clicked+=async (_,_)=>await SelectAsync(category);
            layout.Children.Add(control);
            _buttons["filter:"+category]=control;
        }

        private static Button Tab(string text)
        {
            var b=Ui.Secondary(text);
            b.CornerRadius=10;b.FontSize=10;b.FontAttributes=FontAttributes.Bold;
            b.Padding=new Thickness(0);b.Margin=new Thickness(1,7);
            b.BackgroundColor=Colors.White;
            return b;
        }
        private void AddTab(Grid bottom,string title,int column,string? category)
        {
            var tab=Tab(title);
            tab.Clicked+=async (_,_)=>await SelectAsync(category);
            bottom.Add(tab,column,0);
            _buttons["tab:"+(category??"Home")]=tab;
        }
        #endregion Initialization

        #region Task
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // Returning from Perfil restarts at Destacados; other Back actions preserve the section.
            string? next = _resetOnProfileReturn ? null : _category;
            _resetOnProfileReturn=false;
            await SelectAsync(next);
        }

        private async Task SelectAsync(string? category)
        {
            _category=category;
            _heading.Text=category switch
            {
                "Blog"=>"Blogs", "Guide"=>"Guías", "Job"=>"Empleos", "Event"=>"Eventos", _=>"Destacados"
            };
            foreach(var pair in _buttons)
            {
                bool selected=pair.Key=="tab:"+(category??"Home") || pair.Key=="filter:"+category;
                pair.Value.BackgroundColor=selected ? Ui.Lavender : Colors.White;
            }
            _feedback.Text="Cargando...";
            _cards.Children.Clear();
            var response=await _posts.FeedAsync(category,category is null);
            if(category!=_category)return;
            if(!response.Success)
            {
                _feedback.Text=response.Error;
                return;
            }
            _feedback.Text=string.Empty;
            if(response.Items.Count==0)
            {
                _cards.Children.Add(Ui.Notice(category is null ?
                    "Todavía no hay publicaciones destacadas aprobadas." :
                    "Aún no hay publicaciones aprobadas en esta sección."));
                return;
            }
            foreach(var item in response.Items) _cards.Children.Add(RenderCard(item));
        }

        private Border RenderCard(Post.ViewData post)
        {
            var content=new Grid {ColumnSpacing=12,
                ColumnDefinitions={new ColumnDefinition(new GridLength(105)),new ColumnDefinition(GridLength.Star)}};
            string illustration=post.Category switch
            {
                "Guide"=>"mu_guide.png", "Event"=>"mu_event.png",
                "Job"=>"mu_books.png",_=>"mu_story.png"
            };
            content.Add(new Image {Source=illustration,HeightRequest=108,WidthRequest=105,
                Aspect=Aspect.AspectFill},0,0);
            var info=new VerticalStackLayout {Spacing=5,VerticalOptions=LayoutOptions.Center};
            info.Children.Add(Ui.Text(PostDetailPage.CategoryName(post.Category)+(post.Example?" · Ejemplo":""),11));
            info.Children.Add(Ui.Heading(post.Title,16));
            info.Children.Add(Ui.Text(post.Summary,12));
            content.Add(info,1,0);
            var card=Ui.Card(content,12);
            var tap=new TapGestureRecognizer();
            tap.Tapped+=async (_,_)=>await Navigation.PushAsync(new PostDetailPage(post));
            card.GestureRecognizers.Add(tap);
            return card;
        }
        #endregion Task
    }
}
