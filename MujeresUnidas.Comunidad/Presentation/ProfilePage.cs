using System.Text.Json;
using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.Publications;
using MujeresUnidas.Comunidad.Models.User.Subclasses;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Social-style member profile with cover, public biography, publication history and staff tools.</summary>
    public sealed class ProfilePage : ContentPage
    {
        #region Operation Data
        private readonly ApiClient _api;
        private readonly CommunityMember _member;
        private readonly Post _posts;
        private readonly Entry _name=Ui.Input("Nombre público");
        private readonly Editor _bio=new() {HeightRequest=110,Placeholder="Descripción breve (máximo 300 caracteres)",
            TextColor=Ui.Plum,BackgroundColor=Ui.Lavender,MaxLength=300};
        private readonly Label _message=Ui.Text(string.Empty,12);
        private readonly Label _identity=Ui.Heading("Mi perfil",21);
        private readonly VerticalStackLayout _myPosts=new() {Spacing=10};
        private readonly bool _isStaff;
        #endregion Operation Data

        #region Initialization
        public ProfilePage(ApiClient api)
        {
            _api=api;_member=new CommunityMember(api);_posts=new Post(api);
            Title="Mi perfil";
            _isStaff=api.CurrentUser.ValueKind==JsonValueKind.Object &&
                api.CurrentUser.TryGetProperty("communityRole",out var role) && role.GetString()=="Staff";
            var content=new VerticalStackLayout {Spacing=14,Padding=new Thickness(17,15,17,33)};
            content.Children.Add(new Image {Source="mu_header.png",HeightRequest=125,Aspect=Aspect.AspectFill});
            var head=new HorizontalStackLayout {Spacing=15};
            head.Children.Add(new Image {Source="mu_mark.png",WidthRequest=78,HeightRequest=78,Aspect=Aspect.AspectFit});
            var intro=new VerticalStackLayout {Spacing=5,VerticalOptions=LayoutOptions.Center};
            intro.Children.Add(_identity);
            intro.Children.Add(Ui.Text(_isStaff?"Staff de Comunidad":"Miembro de Comunidad",13));
            head.Children.Add(intro);content.Children.Add(head);
            content.Children.Add(Ui.Notice("Tu información de registro privada no se muestra a otras integrantes."));
            var edit=Ui.Secondary("Modificar mis datos");
            var fields=new VerticalStackLayout {Spacing=9,IsVisible=false};
            fields.Children.Add(Ui.Field(_name));fields.Children.Add(_bio);
            var save=Ui.Primary("Guardar cambios");save.Clicked+=Save;fields.Children.Add(save);
            edit.Clicked+=(_,_)=>fields.IsVisible=!fields.IsVisible;
            content.Children.Add(edit);content.Children.Add(fields);
            var create=Ui.Primary("＋ Crear publicación");
            create.Clicked+=async (_,_)=>await Navigation.PushAsync(new CreatePostPage(api));
            content.Children.Add(create);
            if(_isStaff)
            {
                var moderation=Ui.Secondary("Revisar solicitudes de publicaciones");
                moderation.Clicked+=async (_,_)=>await Navigation.PushAsync(new ModerationPage(api));
                content.Children.Add(moderation);
            }
            content.Children.Add(Ui.Heading("Mis publicaciones",20));
            content.Children.Add(_message);
            content.Children.Add(_myPosts);
            var logout=Ui.Secondary("Cerrar sesión");
            logout.Clicked+=async (_,_)=>
            {
                await _member.SignOutAsync();
                Application.Current!.Windows[0].Page=new NavigationPage(new LoginPage(_api));
            };
            content.Children.Add(logout);
            Content=Ui.Scroll(content);
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
            await ReloadPostsAsync();
        }

        private async Task ReloadPostsAsync()
        {
            var response=await _posts.MineAsync();
            _myPosts.Children.Clear();
            if(!response.Success){_message.Text=response.Error;return;}
            if(response.Items.Count==0){_myPosts.Children.Add(Ui.Notice("Todavía no has creado publicaciones."));return;}
            foreach(var item in response.Items)
            {
                var stack=new VerticalStackLayout {Spacing=6};
                stack.Children.Add(Ui.Heading(item.Title,16));
                stack.Children.Add(Ui.Text(PostDetailPage.CategoryName(item.Category)+" · "+item.Status,12));
                stack.Children.Add(Ui.Text(item.Summary,12));
                var open=Ui.Secondary("Abrir publicación");
                open.Clicked+=async (_,_)=>await Navigation.PushAsync(new PostDetailPage(item));
                stack.Children.Add(open);
                if(!item.Example)
                {
                    var edit=Ui.Secondary("Editar publicación");
                    edit.Clicked+=async (_,_)=>await Navigation.PushAsync(new EditPostPage(_api,item));
                    stack.Children.Add(edit);
                    var remove=Ui.Secondary("Retirar publicación");
                    remove.Clicked+=async (_,_)=>await RemoveAsync(item.Id);
                    stack.Children.Add(remove);
                }
                _myPosts.Children.Add(Ui.Card(stack,12));
            }
        }

        private async Task RemoveAsync(Guid id)
        {
            if(!await DisplayAlertAsync("Retirar", "¿Deseas retirar esta publicación?", "Sí", "Cancelar"))return;
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
