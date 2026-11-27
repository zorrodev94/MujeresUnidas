using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using System.Text.Json;
using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.Messages;
using MujeresUnidas.Comunidad.Models.User.Profiles;
using MujeresUnidas.Comunidad.Models.User.Subclasses;
using MujeresUnidas.Shared.Contracts;

namespace MujeresUnidas.Comunidad.Presentation
{
    /// <summary>
    /// R4.3 native conversation view: separated Community context, real server-backed messages,
    /// rounded avatars, chat bubbles, pull-to-refresh and unobtrusive contextual actions.
    /// Authorized JPEG/PNG, audio-file and MP4 attachments; voice recording remains unsupported.
    /// </summary>
    [UsesData(typeof(global::MujeresUnidas.Comunidad.Models.Messages.Message.MessageData))]
    public sealed class ConversationPage : ContentPage
    {
        #region Operation Data
        private readonly ApiClient _api;
        private readonly Guid _conversationId;
        private readonly string _type;
        private readonly Message _messages;
        private readonly CommunityMember _actor;
        private readonly Conversation _conversations;
        private readonly CommunityDirectory _directory;
        private readonly VerticalStackLayout _timeline=new(){Spacing=11};
        private readonly ScrollView _scroll;
        private readonly RefreshView _refresh;
        private readonly Entry _entry=Ui.Input("Escribe un mensaje...");
        private readonly Label _status=Ui.Text(string.Empty,12);
        private readonly Guid _myId;
        private readonly Image _recipientAvatar=Ui.Avatar(43);
        private readonly Dictionary<Guid, ImageSource?> _photoCache=new();
        private Microsoft.Maui.Dispatching.IDispatcherTimer? _refreshTimer;
        private bool _loading;
        private string _lastSignature=string.Empty;
        private readonly Dictionary<Guid,List<MessageAttachmentDto>> _attachments=new();
        #endregion Operation Data
        #region Initialization
        public ConversationPage(ApiClient api,Guid conversationId,string title,string type="Direct")
        {
            _api=api;_conversationId=conversationId;_type=type;
            _messages=new Message(api);_conversations=new Conversation(api);
            _actor=new CommunityMember(api);_directory=new CommunityDirectory(api);
            _myId=api.CurrentUser.ValueKind==JsonValueKind.Object &&
                api.CurrentUser.TryGetProperty("id",out var id)?id.GetGuid():Guid.Empty;
            Title=title;
            NavigationPage.SetHasNavigationBar(this,false);
            var root=new Grid{BackgroundColor=Ui.Background,RowSpacing=0,RowDefinitions={
                new RowDefinition(GridLength.Auto),new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),new RowDefinition(GridLength.Auto)}};

            var header=new Grid{BackgroundColor=Colors.White,Padding=new Thickness(9,9),ColumnSpacing=10,
                ColumnDefinitions={new ColumnDefinition(new GridLength(45)),new ColumnDefinition(new GridLength(45)),
                    new ColumnDefinition(GridLength.Star),new ColumnDefinition(new GridLength(44))}};
            var back=ActionIcon("mu_icon_back.png","Regresar a mensajes");
            back.Clicked+=async (_,_)=>await Navigation.PopAsync();header.Add(back,0,0);
            header.Add(_recipientAvatar,1,0);
            var nameColumn=new VerticalStackLayout{Spacing=2,VerticalOptions=LayoutOptions.Center};
            string displayTitle=title.StartsWith("Chat con ",StringComparison.OrdinalIgnoreCase)?title[9..]:title;
            nameColumn.Children.Add(Ui.Heading(displayTitle,16));
            nameColumn.Children.Add(Ui.Text(type=="Public"?"Sala pública · No compartas datos sensibles":
                type=="Group"?"Grupo comunitario":"Conversación privada",11));
            header.Add(nameColumn,2,0);
            var more=ActionIcon("mu_icon_more.png","Opciones de la conversación");
            more.Clicked+=async (_,_)=>await ShowOptionsAsync();header.Add(more,3,0);
            root.Add(header,0,0);

            var chatContent=new VerticalStackLayout{Spacing=9,Padding=new Thickness(13,15,13,14)};
            chatContent.Children.Add(Ui.Tag("Mensajes de Comunidad · Independientes de casos y citas"));
            chatContent.Children.Add(_status);
            chatContent.Children.Add(_timeline);
            _scroll=Ui.Scroll(chatContent);
            _scroll.BackgroundColor=Colors.Transparent;
            _refresh=new RefreshView{Content=_scroll,RefreshColor=Ui.Violet,BackgroundColor=Colors.Transparent};
            _refresh.Refreshing+=async (_,_)=>
            {
                try{await ReloadAsync();}finally{_refresh.IsRefreshing=false;}
            };
            // Low-opacity decoration only; real message bubbles remain native controls and readable.
            var conversationBackdrop=new Grid();
            conversationBackdrop.Add(new Image{Source="mu_header.png",Aspect=Aspect.AspectFill,
                Opacity=0.13,InputTransparent=true},0,0);
            conversationBackdrop.Add(_refresh,0,0);
            root.Add(conversationBackdrop,0,1);

            var composer=new Grid{BackgroundColor=Colors.White,ColumnSpacing=8,
                Padding=new Thickness(10,9,10,10),ColumnDefinitions={
                    new ColumnDefinition(new GridLength(45)),
                    new ColumnDefinition(GridLength.Star),new ColumnDefinition(new GridLength(51))}};
            var attach=new ImageButton{Source="mu_icon_paperclip.png",WidthRequest=44,HeightRequest=44,
                BackgroundColor=Ui.Pink,CornerRadius=22,Padding=10};
            SemanticProperties.SetDescription(attach,"Adjuntar fotografía, audio grabado o video MP4");
            attach.Clicked+=async (_,_)=>await AttachAsync(attach);
            composer.Add(attach,0,0);
            composer.Add(Ui.Field(_entry),1,0);
            var send=new ImageButton{Source="mu_icon_send.png",WidthRequest=50,HeightRequest=50,
                BackgroundColor=Ui.Lavender,CornerRadius=25,Padding=12,
                HorizontalOptions=LayoutOptions.Center,VerticalOptions=LayoutOptions.Center};
            SemanticProperties.SetDescription(send,"Enviar mensaje");
            send.Clicked+=async (_,_)=>await SendAsync(send);
            composer.Add(send,2,0);
            root.Add(composer,0,2);
            root.Add(Ui.MainTabs(this,api,"Mensajes"),0,3);
            Content=root;
        }

        /// <summary>Accessible icon action; buttons never imply unavailable video or audio support.</summary>
        private static ImageButton ActionIcon(string file,string description)
        {
            var image=new ImageButton{Source=file,WidthRequest=44,HeightRequest=44,
                BackgroundColor=Ui.Pink,Padding=11,CornerRadius=22};
            SemanticProperties.SetDescription(image,description);
            return image;
        }
        #endregion Initialization

        #region Task
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadRecipientAsync();
            await ReloadAsync();
            // Poll only while this page is visible; no manual "Actualizar" button is required.
            _refreshTimer ??=Dispatcher.CreateTimer();
            _refreshTimer.Interval=TimeSpan.FromSeconds(15);
            _refreshTimer.Tick+=TimerTick;
            _refreshTimer.Start();
        }
        protected override void OnDisappearing()
        {
            if(_refreshTimer is not null)
            {
                _refreshTimer.Stop();
                _refreshTimer.Tick-=TimerTick;
            }
            base.OnDisappearing();
        }
        private async void TimerTick(object? sender,EventArgs e) => await ReloadAsync(false);

        private async Task LoadRecipientAsync()
        {
            if(_type=="Public")return;
            var conversations=await _conversations.GetConversationsAsync();
            var conversation=conversations.Items.FirstOrDefault(c=>c.ConversationId==_conversationId);
            var other=conversation.UserIds?.FirstOrDefault(id=>id!=_myId)??Guid.Empty;
            if(other==Guid.Empty)return;
            var photo=await _directory.ImageAsync(other,"avatar");
            if(photo is not null)_recipientAvatar.Source=Ui.BytesSource(photo);
        }

        private async Task ReloadAsync(bool scrollToEnd=true)
        {
            if(_loading)return;
            _loading=true;
            try
            {
                var response=await _messages.GetMessagesAsync(_conversationId);
                if(!string.IsNullOrWhiteSpace(response.Error))
                {
                    _status.Text=response.Error;
                    return;
                }
                _status.Text=string.Empty;
                var fileReply=await _api.RequestAsync(HttpMethod.Get,$"api/conversations/{_conversationId}/attachments");
                _attachments.Clear();
                if(fileReply.Success && fileReply.Data.ValueKind==JsonValueKind.Array)
                {
                    foreach(var att in JsonProjection.Read<List<MessageAttachmentDto>>(fileReply.Data)??new())
                    {
                        if(!_attachments.TryGetValue(att.MessageId,out var list))_attachments[att.MessageId]=list=new();
                        list.Add(att);
                    }
                }
                var signature=string.Join("|",response.Items.Select(x=>
                    x.MessageId.ToString()+x.Body+x.EditedUtc.ToString()))+"|"+
                    string.Join("|",_attachments.SelectMany(x=>x.Value.Select(v=>v.AssetId.ToString())));
                if(!scrollToEnd && signature==_lastSignature)return;
                _lastSignature=signature;
                _timeline.Children.Clear();
                if(response.Items.Count==0)
                    _timeline.Children.Add(Ui.Notice("Inicia la conversación con un mensaje respetuoso. Los mensajes solo son visibles para sus participantes."));
                foreach(var message in response.Items)
                    _timeline.Children.Add(await CreateBubbleAsync(message));
                if(scrollToEnd)await _scroll.ScrollToAsync(0,_scroll.ContentSize.Height,true);
            }
            finally{_loading=false;}
        }
        private async Task<View> CreateBubbleAsync(Message.MessageData message)
        {
            bool mine=message.SenderUserId==_myId;
            if(!_photoCache.TryGetValue(message.SenderUserId,out var face))
            {
                var data=await _directory.ImageAsync(message.SenderUserId,"avatar");
                face=data is null?null:Ui.BytesSource(data);
                _photoCache[message.SenderUserId]=face;
            }
            var avatar=Ui.Avatar(34);
            if(face is not null)avatar.Source=face;
            var body=new VerticalStackLayout{Spacing=5};
            body.Children.Add(Ui.Text(mine?"Tú":message.SenderName,11));
            body.Children.Add(new Label{Text=message.IsDeleted?"Mensaje eliminado":message.Body,
                TextColor=Ui.Plum,FontSize=14,LineBreakMode=LineBreakMode.WordWrap});
            if(!message.IsDeleted && _attachments.TryGetValue(message.MessageId,out var attachments))
                foreach(var attachment in attachments)
                {
                    if(attachment.MediaType=="Image")
                    {
                        var data=await _api.DownloadAttachmentAsync($"api/conversations/{_conversationId}/attachments/{attachment.AssetId}");
                        if(data.Bytes is not null)
                            body.Children.Add(new Image{Source=Ui.BytesSource(data.Bytes),HeightRequest=180,
                                Aspect=Aspect.AspectFit,MaximumWidthRequest=265});
                    }
                    else
                    {
                        var media=Ui.Secondary(attachment.MediaType=="Audio"?"▶ Reproducir audio adjunto":"▶ Abrir video adjunto");
                        media.Clicked+=async (_,_)=>await OpenMediaAsync(attachment);
                        body.Children.Add(media);
                    }
                }
            body.Children.Add(new Label{Text=message.CreatedUtc.ToLocalTime().ToString("HH:mm")+
                    (message.EditedUtc.HasValue?" · editado":string.Empty),TextColor=Ui.Muted,
                FontSize=10,HorizontalTextAlignment=TextAlignment.End});
            var bubble=Ui.Card(body,13);
            bubble.BackgroundColor=mine?Ui.Lavender:Colors.White;
            bubble.MaximumWidthRequest=460;
            if(mine&&!message.IsDeleted)
            {
                var gesture=new TapGestureRecognizer{NumberOfTapsRequired=2};
                gesture.Tapped+=async (_,_)=>await EditOrDeleteAsync(message);
                bubble.GestureRecognizers.Add(gesture);
                SemanticProperties.SetHint(bubble,"Toca dos veces para editar o eliminar tu mensaje");
            }
            var row=new HorizontalStackLayout{Spacing=7,
                HorizontalOptions=mine?LayoutOptions.End:LayoutOptions.Start,
                VerticalOptions=LayoutOptions.End};
            if(mine) {row.Children.Add(bubble);row.Children.Add(avatar);}
            else {row.Children.Add(avatar);row.Children.Add(bubble);}
            return row;
        }
        private async Task AttachAsync(ImageButton button)
        {
            FileResult? file;
            try
            {
                file=await FilePicker.Default.PickAsync(new PickOptions
                {PickerTitle="Adjuntar JPEG/PNG, MP3/WAV/M4A o MP4 (máximo 15 MB)"});
            }
            catch(Exception ex) when(ex is PermissionException or IOException or NotSupportedException)
            {_status.Text="No se pudo abrir el selector de archivos.";System.Diagnostics.Debug.WriteLine(ex.Message);return;}
            if(file is null)return;
            button.IsEnabled=false;
            try
            {
                var result=await _api.UploadAttachmentAsync($"api/conversations/{_conversationId}/attachments",file);
                _status.Text=result.Success?"Archivo enviado correctamente.":result.Error;
                if(result.Success)await ReloadAsync();
            }
            finally{button.IsEnabled=true;}
        }
        private async Task OpenMediaAsync(MessageAttachmentDto attachment)
        {
            var downloaded=await _api.DownloadAttachmentAsync($"api/conversations/{_conversationId}/attachments/{attachment.AssetId}");
            if(downloaded.Bytes is null){_status.Text="Archivo no disponible o sesión sin permisos.";return;}
            var ext=attachment.MimeType switch{"audio/mp4"=>".m4a","audio/mpeg"=>".mp3",
                "audio/wav"=>".wav","video/mp4"=>".mp4",_=>".bin"};
            var destination=Path.Combine(FileSystem.CacheDirectory,attachment.AssetId.ToString("N")+ext);
            try
            {
                await File.WriteAllBytesAsync(destination,downloaded.Bytes);
                await Launcher.Default.OpenAsync(new OpenFileRequest("Archivo del chat",new ReadOnlyFile(destination)));
            }
            catch(Exception ex) when(ex is IOException or NotSupportedException or InvalidOperationException)
            {_status.Text="No hay reproductor compatible para este archivo.";System.Diagnostics.Debug.WriteLine(ex.Message);}
        }

        private async Task SendAsync(ImageButton button)
        {
            var value=(_entry.Text??string.Empty).Trim();
            if(value.Length is <1 or >2000){_status.Text="El mensaje admite de 1 a 2000 caracteres.";return;}
            button.IsEnabled=false;
            try
            {
                var result=await _actor.SendMessageAsync(_conversationId,value);
                if(result.Success){_entry.Text=string.Empty;await ReloadAsync();}
                else _status.Text=result.Error;
            }
            finally{button.IsEnabled=true;}
        }
        private async Task EditOrDeleteAsync(Message.MessageData message)
        {
            var answer=await DisplayActionSheetAsync("Tu mensaje","Cancelar",null,"Editar","Eliminar");
            if(answer=="Editar")
            {
                var updated=await DisplayPromptAsync("Editar mensaje","Texto","Guardar","Cancelar",message.Body,2000);
                if(string.IsNullOrWhiteSpace(updated))return;
                var result=await _actor.EditMessageAsync(message.MessageId,updated.Trim());
                if(result.Success)await ReloadAsync();else _status.Text=result.Error;
            }
            else if(answer=="Eliminar")
            {
                if(!await DisplayAlertAsync("Eliminar","¿Eliminar tu mensaje?","Sí","Cancelar"))return;
                var result=await _actor.DeleteMessageAsync(message.MessageId);
                if(result.Success)await ReloadAsync();else _status.Text=result.Error;
            }
        }
        private async Task ShowOptionsAsync()
        {
            var options=_type=="Public"?new[]{"Actualizar mensajes","Adjuntos compatibles"}:
                new[]{"Actualizar mensajes","Invitar participante","Abandonar conversación","Adjuntos compatibles"};
            string choice=await DisplayActionSheetAsync("Opciones","Cancelar",null,options);
            switch(choice)
            {
                case "Actualizar mensajes":await ReloadAsync();break;
                case "Invitar participante":await Navigation.PushAsync(new InvitePage(_api,_conversationId));break;
                case "Abandonar conversación":await LeaveAsync();break;
                case "Adjuntos compatibles":await DisplayAlertAsync("Archivos del chat",
                    "Puedes adjuntar fotografías JPEG/PNG, archivos de audio y MP4. Grabar notas de voz directamente, las llamadas y videollamadas requieren una integración posterior.","Entendido");break;
            }
        }
        private async Task LeaveAsync()
        {
            if(!await DisplayAlertAsync("Abandonar","¿Quieres salir de esta conversación?","Salir","Cancelar"))return;
            var response=await _actor.LeaveConversationAsync(_conversationId);
            if(response.Success)await Navigation.PopAsync();else _status.Text=response.Message;
        }
        #endregion Task
    }
}
