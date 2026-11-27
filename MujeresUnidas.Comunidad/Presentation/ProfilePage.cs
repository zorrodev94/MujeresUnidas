using System.Text.Json;
using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.Publications;
using MujeresUnidas.Comunidad.Models.User.Subclasses;
using MujeresUnidas.Comunidad.Models.User.Profiles;
using MujeresUnidas.Comunidad.Models.Badges;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Social-style member profile with cover, public biography, publication history and staff tools.</summary>
    [UsesData(typeof(global::MujeresUnidas.Comunidad.Models.User.Subclasses.CommunityMember.ProfileData))]
    public sealed class ProfilePage : ContentPage
    {
        #region Operation Data
        private readonly ApiClient _api;
        private readonly CommunityMember _member;
        private readonly CommunityDirectory _directory;
        private readonly Guid _userId;
        private readonly Image _avatar=Ui.Avatar(94);
        private readonly Image _cover=new(){Source="mu_header.png",HeightRequest=150,Aspect=Aspect.AspectFill};
        private readonly Label _followerCount=Ui.Heading("0",21);
        private readonly Label _followingCount=Ui.Heading("0",21);
        private readonly Label _publicationCount=Ui.Heading("0",21);
        private readonly Post _posts;
        private readonly Entry _name=Ui.Input("Nombre público");
        private readonly Editor _bio=new() {HeightRequest=110,Placeholder="Descripción breve (máximo 300 caracteres)",
            TextColor=Ui.Plum,BackgroundColor=Ui.Lavender,MaxLength=300};
        private readonly Label _message=Ui.Text(string.Empty,12);
        private readonly Label _identity=Ui.Heading("Mi perfil",21);
        private readonly VerticalStackLayout _myPosts=new() {Spacing=10};
        private readonly VerticalStackLayout _achievements=new() {Spacing=9};
        private readonly BadgeDirectory _badgeDirectory;
        private readonly bool _isStaff;
        #endregion Operation Data
        #region Initialization
        public ProfilePage(ApiClient api)
        {
            _api=api;_member=CommunityMember.FromSession(api);_posts=new Post(api);
            _directory=new CommunityDirectory(api);_badgeDirectory=new BadgeDirectory(api);
            _userId=api.CurrentUser.ValueKind==JsonValueKind.Object && api.CurrentUser.TryGetProperty("id",out var userId)?userId.GetGuid():Guid.Empty;
            Title="Mi perfil";
            _isStaff=_member.CanReviewBlogs;
            NavigationPage.SetHasNavigationBar(this,false);
            var root=new Grid{BackgroundColor=Ui.Background,RowDefinitions={
                new RowDefinition(GridLength.Star),new RowDefinition(GridLength.Auto)}};
            var content=new VerticalStackLayout {Spacing=14,Padding=new Thickness(17,15,17,30),
                MaximumWidthRequest=900,HorizontalOptions=LayoutOptions.Fill};
            content.Children.Add(Ui.Header(this,api,"Mi perfil"));
            var coverGroup=new Grid{HeightRequest=217,
                RowDefinitions={new RowDefinition(new GridLength(150)),new RowDefinition(new GridLength(67))}};
            coverGroup.Add(Ui.Cover(_cover,150),0,0);
            var portrait=new Border{Content=_avatar,Stroke=Colors.White,StrokeThickness=4,
                StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle{CornerRadius=62},
                WidthRequest=104,HeightRequest=104,Padding=0,BackgroundColor=Ui.Lavender,
                HorizontalOptions=LayoutOptions.Center,Margin=new Thickness(0,-53,0,0)};
            coverGroup.Add(portrait,0,1);
            content.Children.Add(coverGroup);
            _identity.HorizontalOptions=LayoutOptions.Center;
            _identity.HorizontalTextAlignment=TextAlignment.Center;
            content.Children.Add(_identity);
            var roleName=_member.PublicRoleLabel;
            var roleBadge=Ui.Tag(roleName,_isStaff);
            roleBadge.HorizontalOptions=LayoutOptions.Center;content.Children.Add(roleBadge);
            var counters=new Grid{ColumnDefinitions={new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Star)},ColumnSpacing=10};
            var followerTile=Statistic(_followerCount,"Seguidores");
            followerTile.GestureRecognizers.Add(new TapGestureRecognizer{Command=new Command(async ()=>
                await Navigation.PushAsync(new PeopleSearchPage(_api,_userId,"followers")))});
            var followingTile=Statistic(_followingCount,"Seguidos");
            followingTile.GestureRecognizers.Add(new TapGestureRecognizer{Command=new Command(async ()=>
                await Navigation.PushAsync(new PeopleSearchPage(_api,_userId,"following")))});
            counters.Add(followerTile,0,0);
            counters.Add(followingTile,1,0);
            counters.Add(Statistic(_publicationCount,"Momentos"),2,0);
            content.Children.Add(Ui.Card(counters,13));
            var actions=new Grid{ColumnSpacing=8,ColumnDefinitions={
                new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Star)}};
            var edit=Ui.Primary("Editar perfil");
            edit.FontSize=13;
            edit.Clicked+=async (_,_)=>await Navigation.PushAsync(new EditCommunityProfilePage(_api));
            actions.Add(edit,0,0);
            var create=Ui.Primary("✎ Publicar momento");
            create.FontSize=12;
            create.Clicked+=async (_,_)=>await Navigation.PushAsync(new CreatePostPage(api,true));
            actions.Add(create,1,0);
            content.Children.Add(actions);
            content.Children.Add(Ui.Notice("Tus datos de registro son privados. Solo compartes el perfil y el contenido que decides publicar."));
            var saved=Ui.Secondary("Mis publicaciones guardadas");
            saved.Clicked+=async (_,_)=>await Navigation.PushAsync(new SavedPostsPage(_api));
            content.Children.Add(Ui.Section("Tu espacio",saved));
            if(_isStaff)
            {
                var moderation=Ui.Secondary("Revisar publicaciones pendientes");
                moderation.Clicked+=async (_,_)=>await Navigation.PushAsync(new ModerationPage(api));
                content.Children.Add(moderation);
            }
            var voluntary=Ui.Secondary("Encuesta de progreso · estudios y empleo");
            voluntary.Clicked+=async (_,_)=>await Navigation.PushAsync(new CommunitySurveyPage(_api));
            content.Children.Add(voluntary);
            content.Children.Add(Ui.Heading("Logros e insignias",21));
            content.Children.Add(Ui.Text("Los logros son reconocimientos, no permisos ni cargos.",12));
            content.Children.Add(Ui.Card(_achievements,12));
            var editorial=Ui.Secondary("Mis blogs, guías y publicaciones generales");
            editorial.Clicked+=async (_,_)=>await Navigation.PushAsync(new MyEditorialPostsPage(_api));
            content.Children.Add(editorial);
            content.Children.Add(Ui.Heading("Mis momentos",21));
            content.Children.Add(Ui.Text("Comparte tus vivencias desde el perfil. Tus artículos generales se crean en Inicio.",12));
            content.Children.Add(_message);
            content.Children.Add(_myPosts);
            var logout=Ui.Secondary("Cerrar sesión");
            logout.Clicked+=async (_,_)=>
            {
                await _member.SignOutAsync();
                Application.Current!.Windows[0].Page=new NavigationPage(new LoginPage(_api));
            };
            content.Children.Add(logout);
            root.Add(Ui.Scroll(content),0,0);
            root.Add(Ui.MainTabs(this,api,"Perfil"),0,1);
            Content=root;
        }
        #endregion Initialization
        #region Task
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            var profile=await _member.GetProfileAsync();
            if(profile.Success)
            {
                _identity.Text=profile.Data.DisplayName;
                _name.Text=profile.Data.DisplayName;
                _bio.Text=profile.Data.Biography;
            }
            else _message.Text=profile.Error;
            await LoadSocialAsync();
            await LoadBadgesAsync();
            await ReloadPostsAsync();
        }

        /// <summary>Renders live profile metrics without injecting synthetic values.</summary>
        private static VerticalStackLayout Statistic(Label value,string caption)
        {
            value.HorizontalTextAlignment=TextAlignment.Center;
            return new VerticalStackLayout{Spacing=3,HorizontalOptions=LayoutOptions.Fill,
                Children={value,new Label{Text=caption,FontSize=11,TextColor=Ui.Muted,
                    HorizontalTextAlignment=TextAlignment.Center}}};
        }

        private async Task LoadSocialAsync()
        {
            var result=await _directory.GetAsync(_userId);
            if(result.Profile.HasValue)
            {
                _followerCount.Text=result.Profile.Value.Followers.ToString();
                _followingCount.Text=result.Profile.Value.Following.ToString();
            }
            var avatar=await _directory.ImageAsync(_userId,"avatar");
            if(avatar is not null)_avatar.Source=Ui.BytesSource(avatar);
            var cover=await _directory.ImageAsync(_userId,"cover");
            if(cover is not null)_cover.Source=Ui.BytesSource(cover);
        }

        private async Task PickImageAsync(string type)
        {
            try
            {
                var result=await FilePicker.Default.PickAsync(new PickOptions
                {PickerTitle=type=="avatar"?"Elige una fotografía":"Elige una portada",
                    FileTypes=FilePickerFileType.Images});
                if(result is null)return;
                var saved=await _directory.UploadAsync(type,result);
                _message.Text=saved.Success?"Imagen actualizada.":saved.Error;
                if(saved.Success)await LoadSocialAsync();
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or NotSupportedException)
            { _message.Text="No se pudo abrir el selector de imágenes."; }
        }

        private async Task LoadBadgesAsync()
        {
            _achievements.Children.Clear();
            var response=await _badgeDirectory.ForUserAsync(_userId);
            if(!response.Success)
            {
                _achievements.Children.Add(Ui.Notice("No fue posible cargar tus logros: "+response.Error));return;
            }
            if(response.Items.Count==0)
            {
                _achievements.Children.Add(Ui.Text("Aún no tienes insignias otorgadas. Los reconocimientos se asignan desde Gestión.",12));return;
            }
            _achievements.Children.Add(BadgeDirectory.Gallery(response.Items));
        }

        private async Task ReloadPostsAsync()
        {
            var response=await _posts.MineAsync();
            _myPosts.Children.Clear();
            if(!response.Success){_message.Text=response.Error;return;}
            var moments=response.Items.Where(post=>post.Category=="Moment").ToList();
            _publicationCount.Text=moments.Count.ToString();
            if(moments.Count==0){_myPosts.Children.Add(Ui.Notice("Todavía no has compartido Momentos desde tu perfil."));return;}
            foreach(var item in moments)
            {
                var stack=new VerticalStackLayout {Spacing=9};
                var heading=new HorizontalStackLayout{Spacing=10};
                heading.Children.Add(Ui.Avatar(40));
                var byline=new VerticalStackLayout{Spacing=1};
                byline.Children.Add(Ui.Heading(_identity.Text??"Mi momento",15));
                byline.Children.Add(Ui.Text("Momento · "+item.CreatedUtc.ToLocalTime().ToString("dd MMM yyyy")+" · "+item.Status,11));
                heading.Children.Add(byline);stack.Children.Add(heading);
                stack.Children.Add(Ui.RichParagraph(item.Body,14));
                var media=await _posts.MediaAsync(item.Id);
                if(media.Items.Count>0)
                {
                    var photo=await _posts.ImageAsync(item.Id,media.Items[0]);
                    if(photo is not null)stack.Children.Add(Ui.Cover(new Image
                    {Source=Ui.BytesSource(photo),HeightRequest=178,Aspect=Aspect.AspectFill},178));
                }
                var open=Ui.Secondary("♡ Ver comentarios y reacciones");
                open.Clicked+=async (_,_)=>await Navigation.PushAsync(new PostDetailPage(item,_api));
                stack.Children.Add(open);
                if(!item.Example)
                {
                    var controls=new Grid{ColumnSpacing=8,ColumnDefinitions={new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Star)}};
                    var edit=Ui.Secondary("Editar");
                    edit.Clicked+=async (_,_)=>await Navigation.PushAsync(new EditPostPage(_api,item));
                    controls.Add(edit,0,0);
                    var remove=Ui.Secondary("Retirar");
                    remove.Clicked+=async (_,_)=>await RemoveAsync(item.Id);
                    controls.Add(remove,1,0);
                    stack.Children.Add(controls);
                }
                _myPosts.Children.Add(Ui.Card(stack,14));
            }
        }

        private async Task RemoveAsync(Guid id)
        {
            if(!await DisplayAlertAsync("Retirar", "¿Enviar a Gestión la solicitud de retiro de tu publicación? Personal autorizado puede retirar sus blogs directamente.", "Sí", "Cancelar"))return;
            var response=await _posts.DeletePostAsync(id);
            _message.Text=response.Message;
            if(response.Success)await ReloadPostsAsync();
        }

        private async void Save(object? sender,EventArgs args)
        {
            if(sender is not Button button)return;
            if((_name.Text??string.Empty).Trim().Length<2)
            { _message.Text="El nombre requiere al menos dos caracteres.";return; }
            button.IsEnabled=false;
            try
            {
                var result=await _member.UpdateProfileAsync(new CommunityMember.ProfileData
                {DisplayName=_name.Text!.Trim(),Biography=_bio.Text});
                _message.Text=result.Message;
                if(result.Success)_identity.Text=_name.Text;
            }
            finally {button.IsEnabled=true;}
        }
        #endregion Task
    }
}
