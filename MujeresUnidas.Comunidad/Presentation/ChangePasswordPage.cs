using MujeresUnidas.Comunidad.Connection;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>Changes a private password and requires login again after the server revokes sessions.</summary>
    public sealed class ChangePasswordPage : ContentPage
    {
        #region Initialization
        public ChangePasswordPage(ApiClient api)
        {
            Title="Cambiar contraseña";NavigationPage.SetHasNavigationBar(this,false);
            var old=Ui.Input("Contraseña actual");old.IsPassword=true;
            var newer=Ui.Input("Contraseña nueva (10 caracteres mínimo)");newer.IsPassword=true;
            var repeat=Ui.Input("Repite la contraseña nueva");repeat.IsPassword=true;
            var status=Ui.Text(string.Empty,12);
            var content=new VerticalStackLayout{Padding=new Thickness(20),Spacing=13};
            content.Children.Add(Ui.Back(this));content.Children.Add(Ui.Heading("Protege tu cuenta",24));
            content.Children.Add(Ui.Notice("Cambiar la contraseña cerrará todas las sesiones, incluido este dispositivo."));
            foreach(var field in new[]{old,newer,repeat})content.Children.Add(Ui.Field(field));
            var save=Ui.Primary("Actualizar contraseña");content.Children.Add(save);content.Children.Add(status);
            save.Clicked+=async (_,_)=>
            {
                if(newer.Text?.Length<10 || newer.Text!=repeat.Text)
                {status.Text="La nueva contraseña debe tener al menos 10 caracteres y coincidir.";return;}
                save.IsEnabled=false;
                try
                {
                    var result=await api.RequestAsync(HttpMethod.Put,"api/auth/change-password",
                        new{currentPassword=old.Text,newPassword=newer.Text});
                    if(!result.Success){status.Text=result.Error;return;}
                    api.SignOut();
                    await DisplayAlertAsync("Contraseña actualizada","Las sesiones anteriores ya no pueden acceder. Vuelve a iniciar sesión.","Aceptar");
                    Application.Current!.Windows[0].Page=new NavigationPage(new LoginPage(api));
                }
                finally{save.IsEnabled=true;}
            };
            Content=Ui.Scroll(content);
        }
        #endregion Initialization
    }
}
