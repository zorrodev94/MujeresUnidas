using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.Publications;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Displays the authenticated member's approved bookmarked publications.</summary>
    [UsesData(typeof(Post.ViewData))]
    public sealed class SavedPostsPage : ContentPage
    {
        #region Operation Data
        private readonly ApiClient _api;
        private readonly Post _posts;
        private readonly VerticalStackLayout _results=new(){Spacing=11};
        private readonly Label _status=Ui.Text(string.Empty,12);
        #endregion Operation Data
        #region Initialization
        public SavedPostsPage(ApiClient api)
        {
            _api=api;_posts=new Post(api);
            Title="Guardados";NavigationPage.SetHasNavigationBar(this,false);
            var body=new VerticalStackLayout{Spacing=14,Padding=new Thickness(17,12,17,28)};
            body.Children.Add(Ui.Header(this,api,"Biblioteca personal"));
            body.Children.Add(Ui.Back(this));
            body.Children.Add(Ui.Heading("Mis publicaciones guardadas",24));
            body.Children.Add(Ui.Text("Tus lecturas guardadas, disponibles mientras sigan aprobadas.",12));
            body.Children.Add(_status);body.Children.Add(_results);
            var root=new Grid{BackgroundColor=Ui.Background,RowDefinitions={
                new RowDefinition(GridLength.Star),new RowDefinition(GridLength.Auto)}};
            root.Add(Ui.Scroll(body),0,0);root.Add(Ui.MainTabs(this,api,string.Empty),0,1);Content=root;
        }
        #endregion Initialization
        #region Arrays
        #endregion Arrays
        #region Structs
        #endregion Structs
        #region Task
        protected override async void OnAppearing()
        {
            base.OnAppearing();_status.Text="Cargando publicaciones guardadas...";
            _results.Children.Clear();
            var result=await _posts.SavedAsync();
            _status.Text=result.Error;
            if(!result.Success)return;
            if(result.Items.Count==0){_results.Children.Add(Ui.Notice("Todavía no guardas publicaciones."));return;}
            foreach(var post in result.Items)
            {
                var line=new VerticalStackLayout{Spacing=6};
                line.Children.Add(Ui.Tag(PostDetailPage.CategoryName(post.Category)));
                line.Children.Add(Ui.Heading(post.Title,18));
                line.Children.Add(Ui.Text(post.Summary,12));
                var read=Ui.Secondary("Abrir publicación");
                read.Clicked+=async (_,_)=>await Navigation.PushAsync(new PostDetailPage(post,_api));
                line.Children.Add(read);_results.Children.Add(Ui.Card(line,12));
            }
        }
        #endregion Task
    }
}
