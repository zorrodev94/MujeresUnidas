using MujeresUnidas.Comunidad.Models.User.Subclasses;
using MujeresUnidas.Comunidad.Connection;

namespace MujeresUnidas.Comunidad.Presentation
{
/// <summary>Edits public profile fields only; confidential identifiers remain private to the institution.</summary>
public sealed class ProfilePage : ContentPage
{
    private readonly ApiClient _api;
    private readonly CommunityMember _member;
    private readonly Entry _name=Ui.Input("Nombre para mostrar");
    private readonly Editor _bio=new() {HeightRequest=130,Placeholder="Cuéntanos sobre ti (máximo 300 caracteres)",TextColor=Ui.Plum,
        BackgroundColor=Ui.Lavender,MaxLength=300};
    private readonly Label _message=Ui.Text("",12);

    public ProfilePage(ApiClient api)
    {
        _api=api;_member=new CommunityMember(api);Title="Mi perfil";
        var content=new VerticalStackLayout {Padding=new Thickness(22,17,22,30),Spacing=15,MaximumWidthRequest=570,HorizontalOptions=LayoutOptions.Center};
        content.Children.Add(Ui.Logo(115));
        content.Children.Add(Ui.Heading("Mi perfil",28));
        content.Children.Add(Ui.Text("Personaliza lo que otras integrantes pueden ver. Tu CURP y tus datos de registro no se muestran públicamente."));
        var fields=new VerticalStackLayout {Spacing=10};
        fields.Children.Add(Ui.Heading("Información pública",20));
        fields.Children.Add(Ui.Field(_name));
        fields.Children.Add(_bio);
        var save=Ui.Primary("Guardar cambios");save.Clicked+=Save;fields.Children.Add(save);
        fields.Children.Add(_message);
        content.Children.Add(Ui.Card(fields));
        var logout=Ui.Secondary("Cerrar sesión");
        logout.Clicked+=(_,_)=>{_member.SignOut();Application.Current!.Windows[0].Page=new NavigationPage(new LoginPage(_api));};
        content.Children.Add(logout);
        Content=Ui.Scroll(content);

    }
    #region Task
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var response=await _member.GetProfileAsync();
        if(response.Success)
        {
            _name.Text=response.Data.DisplayName;
            _bio.Text=response.Data.Biography??string.Empty;
        }
        else _message.Text=response.Error;
    }

    private async void Save(object? sender,EventArgs args)
    {
        if(sender is not Button button)return;
        if((_name.Text??string.Empty).Trim().Length<2)
        { _message.Text="El nombre para mostrar requiere al menos dos caracteres."; return; }
        button.IsEnabled=false;
        try
        {
            var result=await _member.UpdateProfileAsync(new CommunityMember.ProfileData
            {
                DisplayName=_name.Text!.Trim(),Biography=_bio.Text
            });
            _message.Text=result.Message;
        }
        finally {button.IsEnabled=true;}
    }
    #endregion Task
}
}
