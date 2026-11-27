using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.Publications;

namespace MujeresUnidas.Comunidad.Presentation;

/// <summary>Personal editorial library, deliberately separate from the Moment-only profile feed.</summary>
internal sealed class MyEditorialPostsPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly Post _posts;
    private readonly VerticalStackLayout _cards = new() {Spacing=10};
    private readonly Label _status=Ui.Text(string.Empty,12);
    public MyEditorialPostsPage(ApiClient api)
    {
        _api=api;_posts=new Post(api);Title="Mis publicaciones";
        NavigationPage.SetHasNavigationBar(this,false);
        var body=new VerticalStackLayout{Padding=new Thickness(16,14,16,28),Spacing=12};
        body.Children.Add(Ui.Back(this));
        body.Children.Add(Ui.Heading("Mis publicaciones generales",23));
        body.Children.Add(Ui.Notice("Blogs, guías, empleos y eventos se crean desde Inicio. Los Momentos se crean solo en tu perfil."));
        body.Children.Add(_status);body.Children.Add(_cards);
        var root=new Grid{BackgroundColor=Ui.Background,RowDefinitions={new RowDefinition(GridLength.Star),new RowDefinition(GridLength.Auto)}};
        root.Add(Ui.Scroll(body),0,0);
        root.Add(Ui.MainTabs(this,api,string.Empty),0,1);
        Content=root;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();_cards.Children.Clear();
        var response=await _posts.MineAsync();
        if(!response.Success){_status.Text=response.Error;return;}
        _status.Text=string.Empty;
        var articles=response.Items.Where(p=>p.Category!="Moment").ToList();
        if(articles.Count==0){_cards.Children.Add(Ui.Notice("No tienes publicaciones editoriales todavía."));return;}
        foreach(var post in articles)
        {
            var panel=new VerticalStackLayout{Spacing=8};
            panel.Children.Add(Ui.Tag(PostDetailPage.CategoryName(post.Category)+" · "+post.Status));
            panel.Children.Add(Ui.Heading(post.Title,17));
            panel.Children.Add(Ui.Text(post.Summary,12));
            var open=Ui.Secondary("Abrir artículo");
            open.Clicked+=async (_,_)=>await Navigation.PushAsync(new PostDetailPage(post,_api));
            panel.Children.Add(open);
            if(!post.Example)
            {
                var edit=Ui.Secondary("Editar artículo");
                edit.Clicked+=async (_,_)=>await Navigation.PushAsync(new EditPostPage(_api,post));
                panel.Children.Add(edit);
            }
            _cards.Children.Add(Ui.Card(panel,12));
        }
    }
}
