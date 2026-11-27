using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.User.Profiles;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Searches only approved Community accounts; never exposes registration identifiers.</summary>
    public sealed class PeopleSearchPage : ContentPage
    {
        #region Operation Data
        private readonly ApiClient _api;
        private readonly CommunityDirectory _directory;
        private readonly Entry _query=Ui.Input("Buscar persona por nombre");
        private readonly VerticalStackLayout _results=new(){Spacing=11};
        private readonly Label _status=Ui.Text(string.Empty,12);
        private readonly Guid? _source;
        private readonly string? _relation;
        #endregion Operation Data
        #region Initialization
        public PeopleSearchPage(ApiClient api,Guid? source=null,string? relation=null)
        {
            _api=api;_directory=new CommunityDirectory(api);_source=source;_relation=relation;
            Title=relation=="followers"?"Seguidores":relation=="following"?"Seguidos":"Buscar personas";
            NavigationPage.SetHasNavigationBar(this,false);
            var root=new Grid{BackgroundColor=Ui.Background,RowDefinitions={
                new RowDefinition(GridLength.Star),new RowDefinition(GridLength.Auto)}};
            var content=new VerticalStackLayout{Padding=new Thickness(16,18,16,35),Spacing=13};
            content.Children.Add(Ui.Header(this,api,"Personas y perfiles"));
            if(relation is not null)content.Children.Add(Ui.Back(this));
            content.Children.Add(Ui.Heading(Title,24));
            content.Children.Add(Ui.Text("Conoce a otras integrantes; sus datos privados no se muestran.",12));
            if(relation is null)
            {
                content.Children.Add(Ui.Field(_query));
                var search=Ui.Primary("⌕ Buscar");search.Clicked+=async (_,_)=>await LoadAsync();
                content.Children.Add(search);
            }
            content.Children.Add(_status);content.Children.Add(_results);
            root.Add(Ui.Scroll(content),0,0);
            root.Add(Ui.MainTabs(this,api,string.Empty),0,1);
            Content=root;
        }
        #endregion Initialization
        #region Task
        protected override async void OnAppearing(){base.OnAppearing();await LoadAsync();}
        private async Task LoadAsync()
        {
            _results.Children.Clear();_status.Text="Buscando...";
            var response=_relation switch
            {
                "followers" when _source.HasValue=>await _directory.FollowersAsync(_source.Value),
                "following" when _source.HasValue=>await _directory.FollowingAsync(_source.Value),
                _=>await _directory.SearchAsync((_query.Text??string.Empty).Trim())
            };
            _status.Text=string.IsNullOrEmpty(response.Error)?$"{response.Items.Count} perfiles":"Error: "+response.Error;
            foreach(var person in response.Items)
            {
                var row=new VerticalStackLayout{Spacing=7};
                var identity=new HorizontalStackLayout{Spacing=10};
                var avatar=Ui.Icon("nav_profile.png",52);
                identity.Children.Add(avatar);
                var details=new VerticalStackLayout{Spacing=5};
                details.Children.Add(Ui.Heading(person.DisplayName,17));
                details.Children.Add(Ui.Tag(person.CommunityRole=="Staff"?"Staff de Comunidad":"Miembro de Comunidad",person.CommunityRole=="Staff"));
                identity.Children.Add(details);row.Children.Add(identity);
                var photo=await _directory.ImageAsync(person.UserId,"avatar");
                if(photo is not null)avatar.Source=Ui.BytesSource(photo);
                if(!string.IsNullOrWhiteSpace(person.Biography))row.Children.Add(Ui.Text(person.Biography,12));
                var open=Ui.Secondary("Visitar perfil");
                Guid id=person.UserId;
                open.Clicked+=async (_,_)=>await Navigation.PushAsync(new PublicProfilePage(_api,id));
                row.Children.Add(open);_results.Children.Add(Ui.Card(row,13));
            }
            if(response.Items.Count==0 && string.IsNullOrWhiteSpace(response.Error))
                _results.Children.Add(Ui.Notice("No hay perfiles disponibles con esa búsqueda."));
        }
        #endregion Task
    }
}
