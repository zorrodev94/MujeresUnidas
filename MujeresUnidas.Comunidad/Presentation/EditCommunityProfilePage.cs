using System.Text.Json;
using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.User.Subclasses;
using MujeresUnidas.Comunidad.Models.User.Profiles;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Dedicated profile editor with guarded image upload and server persistence.</summary>
    [UsesData(typeof(CommunityMember.ProfileData))]
    public sealed class EditCommunityProfilePage : ContentPage
    {
        #region Operation Data
        private readonly ApiClient _api;
        private readonly CommunityMember _member;
        private readonly CommunityDirectory _directory;
        private readonly Entry _displayName=Ui.Input("Nombre visible");
        private readonly Editor _bio=new(){HeightRequest=140,MaxLength=300,
            Placeholder="Cuéntanos un poco de ti (máximo 300 caracteres)",TextColor=Ui.Plum,BackgroundColor=Ui.Pink};
        private readonly Label _notice=Ui.Text(string.Empty,12);
        private readonly Guid _id;
        private readonly Image _avatar=Ui.Avatar(88);
        private readonly Image _cover=new(){Source="mu_header.png",HeightRequest=150,Aspect=Aspect.AspectFill};
        #endregion Operation Data
        #region Initialization
        public EditCommunityProfilePage(ApiClient api)
        {
            _api=api;_member=new CommunityMember(api);_directory=new CommunityDirectory(api);
            _id=api.CurrentUser.ValueKind==JsonValueKind.Object &&
                api.CurrentUser.TryGetProperty("id",out var account)?account.GetGuid():Guid.Empty;
            Title="Editar perfil";NavigationPage.SetHasNavigationBar(this,false);
            var root=new Grid{BackgroundColor=Ui.Background,RowDefinitions={
                new RowDefinition(GridLength.Star),new RowDefinition(GridLength.Auto)}};
            var body=new VerticalStackLayout{Padding=new Thickness(17,13,17,35),Spacing=14};
            body.Children.Add(Ui.Back(this));body.Children.Add(Ui.Heading("Personaliza tu perfil",25));
            body.Children.Add(Ui.Text("Puedes cambiar tus fotografías sin modificar el rol ni tus permisos.",13));
            var art=new VerticalStackLayout{Spacing=10,Children={_cover,_avatar}};
            body.Children.Add(Ui.Card(art,11));
            var buttons=new HorizontalStackLayout{Spacing=8};
            var photo=Ui.Secondary("Cambiar foto");photo.Clicked+=async (_,_)=>await UploadAsync("avatar");
            var banner=Ui.Secondary("Cambiar portada");banner.Clicked+=async (_,_)=>await UploadAsync("cover");
            buttons.Children.Add(photo);buttons.Children.Add(banner);body.Children.Add(buttons);
            var fields=new VerticalStackLayout{Spacing=10};
            fields.Children.Add(Ui.Text("Nombre público",12));fields.Children.Add(Ui.Field(_displayName));
            fields.Children.Add(Ui.Text("Biografía",12));fields.Children.Add(Ui.Card(_bio,10));
            var save=Ui.Primary("Guardar cambios");save.Clicked+=Save;fields.Children.Add(save);
            body.Children.Add(Ui.Section("Datos visibles",fields));
            var personal=Ui.Secondary("Mis datos privados");
            personal.Clicked+=async (_,_)=>await Navigation.PushAsync(new PersonalDetailsPage(_api));
            body.Children.Add(personal);
            var security=Ui.Secondary("Seguridad y contraseña");
            security.Clicked+=async (_,_)=>await Navigation.PushAsync(new ChangePasswordPage(_api));
            body.Children.Add(security);
            body.Children.Add(Ui.Notice("El nombre y la biografía son públicos. CURP, dirección y contraseña no se muestran a otras integrantes."));
            body.Children.Add(_notice);
            root.Add(Ui.Scroll(body),0,0);
            root.Add(Ui.MainTabs(this,api,string.Empty),0,1);Content=root;
        }
        #endregion Initialization
        #region Task
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            var profile=await _member.GetProfileAsync();
            if(profile.Success){_displayName.Text=profile.Data.DisplayName;_bio.Text=profile.Data.Biography;}
            else _notice.Text=profile.Error;
            await RefreshImagesAsync();
        }
        private async Task RefreshImagesAsync()
        {
            var avatar=await _directory.ImageAsync(_id,"avatar");
            if(avatar is not null)_avatar.Source=Ui.BytesSource(avatar);
            var cover=await _directory.ImageAsync(_id,"cover");
            if(cover is not null)_cover.Source=Ui.BytesSource(cover);
        }
        private async Task UploadAsync(string kind)
        {
            try
            {
                var file=await FilePicker.Default.PickAsync(new PickOptions
                {PickerTitle=kind=="avatar"?"Selecciona una foto":"Selecciona una portada",
                    FileTypes=FilePickerFileType.Images});
                if(file is null)return;
                var reply=await _directory.UploadAsync(kind,file);
                _notice.Text=reply.Success?"Imagen actualizada.":reply.Error;
                if(reply.Success)await RefreshImagesAsync();
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {_notice.Text="No pudimos abrir esa imagen.";}
        }
        private async void Save(object? sender,EventArgs e)
        {
            if(sender is not Button button)return;
            var name=(_displayName.Text??string.Empty).Trim();
            if(name.Length is <2 or >100){_notice.Text="Nombre: de 2 a 100 caracteres.";return;}
            button.IsEnabled=false;
            try
            {
                var response=await _member.UpdateProfileAsync(new CommunityMember.ProfileData
                {DisplayName=name,Biography=(_bio.Text??string.Empty).Trim()});
                _notice.Text=response.Message;
                if(response.Success)await Navigation.PopAsync();
            }
            finally{button.IsEnabled=true;}
        }
        #endregion Task
    }
}
