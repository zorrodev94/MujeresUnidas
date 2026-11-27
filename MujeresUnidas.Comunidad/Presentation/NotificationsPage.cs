using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.Notifications;

namespace MujeresUnidas.Comunidad.Presentation
{
    public sealed class NotificationsPage : ContentPage
    {
        #region Operation Data
        private readonly Notification _notifications;
        private readonly VerticalStackLayout _items=new(){Spacing=10};
        private readonly Label _message=Ui.Text(string.Empty,12);
        #endregion Operation Data
        #region Initialization
        public NotificationsPage(ApiClient api)
        {
            _notifications=new Notification(api);Title="Notificaciones";
            var content=new VerticalStackLayout{Padding=new Thickness(15,15,15,28),Spacing=13};
            content.Children.Add(Ui.Header(this,api,"Notificaciones"));
            content.Children.Add(Ui.Heading("Notificaciones",25));
            content.Children.Add(_message);content.Children.Add(_items);
            var root=new Grid{BackgroundColor=Ui.Background,RowDefinitions={
                new RowDefinition(GridLength.Star),new RowDefinition(GridLength.Auto)}};
            root.Add(Ui.Scroll(content),0,0);
            root.Add(Ui.MainTabs(this,api,string.Empty),0,1);
            Content=root;
        }
        #endregion Initialization
        #region Task
        protected override async void OnAppearing(){base.OnAppearing();await ReloadAsync();}
        private async Task ReloadAsync()
        {
            _items.Children.Clear();
            var result=await _notifications.ListAsync();
            _message.Text=result.Error;
            if(result.Items.Count==0 && string.IsNullOrEmpty(result.Error))
                _items.Children.Add(Ui.Notice("Por ahora no tienes notificaciones."));
            foreach(var item in result.Items)
            {
                var content=new VerticalStackLayout{Spacing=7};
                content.Children.Add(Ui.Heading(item.Title+(item.IsRead?"":" · Nueva"),15));
                content.Children.Add(Ui.Text(item.CreatedUtc.ToLocalTime().ToString("g"),11));
                if(!item.IsRead)
                {
                    var read=Ui.Secondary("Marcar como leída");
                    read.Clicked+=async (_,_)=>{if(await _notifications.MarkReadAsync(item.NotificationId))await ReloadAsync();};
                    content.Children.Add(read);
                }
                _items.Children.Add(Ui.Card(content,13));
            }
        }
        #endregion Task
    }
}
