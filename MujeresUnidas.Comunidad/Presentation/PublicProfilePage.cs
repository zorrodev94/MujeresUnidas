using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.Messages;
using MujeresUnidas.Comunidad.Models.Publications;
using MujeresUnidas.Comunidad.Models.User.Profiles;
using MujeresUnidas.Comunidad.Models.Badges;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Public Community profile; professional/owner data from Gestión is never exposed here.</summary>
    public sealed class PublicProfilePage : ContentPage
    {
        #region Operation Data
        private readonly ApiClient _api;
        private readonly Guid _id;
        private readonly CommunityDirectory _directory;
        private readonly VerticalStackLayout _content=new(){Spacing=13,Padding=new Thickness(16,10,16,35)};
        private readonly Label _status=Ui.Text(string.Empty,12);
        #endregion Operation Data
        #region Initialization
        public PublicProfilePage(ApiClient api,Guid id)
        {
            _api=api;_id=id;_directory=new CommunityDirectory(api);
            Title="Perfil de Comunidad";NavigationPage.SetHasNavigationBar(this,false);
            _content.Children.Add(_status);
            var root=new Grid{BackgroundColor=Ui.Background,RowDefinitions={
                new RowDefinition(GridLength.Star),new RowDefinition(GridLength.Auto)}};
            root.Add(Ui.Scroll(_content),0,0);
            root.Add(Ui.MainTabs(this,api,string.Empty),0,1);
            Content=root;
        }
        #endregion Initialization
        #region Task
        protected override async void OnAppearing(){base.OnAppearing();await LoadAsync();}
        private async Task LoadAsync()
        {
            _content.Children.Clear();_content.Children.Add(Ui.Back(this));
            _status.Text="Cargando perfil...";
            var result=await _directory.GetAsync(_id);
            if(result.Profile is null){_content.Children.Add(Ui.Notice(result.Error));return;}
            var data=result.Profile.Value;
            var cover=new Image{Source="mu_header.png",HeightRequest=140,Aspect=Aspect.AspectFill};
            _content.Children.Add(Ui.Cover(cover,140));
            var image=await _directory.ImageAsync(_id,"cover");
            if(image is not null)cover.Source=Ui.BytesSource(image);
            var face=Ui.Avatar(82);
            var avatar=await _directory.ImageAsync(_id,"avatar");
            if(avatar is not null)face.Source=Ui.BytesSource(avatar);
            _content.Children.Add(face);
            _content.Children.Add(Ui.Heading(data.DisplayName,23));
            _content.Children.Add(Ui.Tag(data.CommunityRole=="Staff"?"Staff de Comunidad":data.CommunityRole=="Professional"?"Profesional institucional":"Miembro de Comunidad",data.CommunityRole=="Staff"));
            _content.Children.Add(Ui.Text(data.Biography,13));
            var counts=new HorizontalStackLayout{Spacing=9};
            var followers=Ui.Secondary($"{data.Followers} seguidores");
            followers.Clicked+=async (_,_)=>await Navigation.PushAsync(new PeopleSearchPage(_api,_id,"followers"));
            var following=Ui.Secondary($"{data.Following} seguidos");
            following.Clicked+=async (_,_)=>await Navigation.PushAsync(new PeopleSearchPage(_api,_id,"following"));
            counts.Children.Add(followers);counts.Children.Add(following);_content.Children.Add(counts);
            if(!data.IsSelf)
            {
                var action=new HorizontalStackLayout{Spacing=8};
                var follow=Ui.Primary(data.IsFollowed?"Dejar de seguir":"Seguir");
                follow.Clicked+=async (_,_)=>
                {
                    var update=await _directory.FollowAsync(_id,!data.IsFollowed);
                    if(!update.Success)await DisplayAlertAsync("Perfil",update.Error,"Aceptar");
                    else await LoadAsync();
                };
                var message=Ui.Secondary("Mensaje");
                message.Clicked+=async (_,_)=>
                {
                    var conversations=new Conversation(_api);
                    var created=await conversations.CreateConversationAsync(new Conversation.CreateData
                    {Title="Chat con "+data.DisplayName,Type="Direct",UserIds=new List<Guid>{_id}});
                    if(created.Id.HasValue)await Navigation.PushAsync(new ConversationPage(_api,created.Id.Value,"Chat con "+data.DisplayName));
                    else await DisplayAlertAsync("Mensajes",created.Error,"Aceptar");
                };
                action.Children.Add(follow);action.Children.Add(message);_content.Children.Add(action);
            }
            else
            {
                var edit=Ui.Secondary("Modificar mi perfil");
                edit.Clicked+=async (_,_)=>await Navigation.PushAsync(new ProfilePage(_api));
                _content.Children.Add(edit);
            }
            _content.Children.Add(Ui.Heading("Logros e insignias",19));
            var badges=await new BadgeDirectory(_api).ForUserAsync(_id);
            if(badges.Success)
                _content.Children.Add(badges.Items.Count>0?BadgeDirectory.Gallery(badges.Items):Ui.Notice("Sin reconocimientos asignados todavía."));
            _content.Children.Add(Ui.Heading("Momentos de su perfil",19));
            var posts=new Post(_api);
            var feed=await posts.ByAuthorAsync(_id);
            if(!feed.Success)_content.Children.Add(Ui.Notice(feed.Error));
            else if(feed.Items.Count==0)_content.Children.Add(Ui.Text("No tiene publicaciones públicas todavía.",12));
            else foreach(var post in feed.Items.Where(p=>p.Category=="Moment"))
            {
                var card=new VerticalStackLayout{Spacing=4};
                card.Children.Add(Ui.Text("Momento · "+post.CreatedUtc.ToLocalTime().ToString("dd MMM yyyy"),12));
                card.Children.Add(Ui.RichParagraph(post.Body,14));
                var media=await posts.MediaAsync(post.Id);
                if(media.Items.Count>0)
                {
                    var bytes=await posts.ImageAsync(post.Id,media.Items[0]);
                    if(bytes is not null)card.Children.Add(Ui.Cover(new Image{Source=Ui.BytesSource(bytes),HeightRequest=165,Aspect=Aspect.AspectFill},165));
                }
                var open=Ui.Secondary("Leer publicación");
                open.Clicked+=async (_,_)=>await Navigation.PushAsync(new PostDetailPage(post,_api));
                card.Children.Add(open);_content.Children.Add(Ui.Card(card,12));
            }
        }
        #endregion Task
    }
}
