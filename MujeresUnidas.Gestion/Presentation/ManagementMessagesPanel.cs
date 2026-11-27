using System.Text.Json;
using MujeresUnidas.Gestion.Connection;

namespace MujeresUnidas.Gestion.Presentation
{
    /// <summary>Institutional direct messages for active staff and owners; no Community conversations.</summary>
    public sealed class ManagementMessagesPanel : UserControl
    {
        #region Operation Data
        private readonly ApiConnection _api;
        private readonly ListView _inbox;
        private readonly ListView _colleagues;
        private readonly TextBox _search;
        private readonly TextBox _draft;
        private readonly FlowLayoutPanel _timeline;
        private readonly Label _caption;
        private readonly Label _status;
        private readonly Button _send;
        private readonly Guid _actorId;
        private Guid _conversationId;
        private bool _busy;
        #endregion Operation Data
        #region Initialization
        public ManagementMessagesPanel(ApiConnection api)
        {
            _api=api;
            _actorId=api.CurrentUser.ValueKind==JsonValueKind.Object &&
                api.CurrentUser.TryGetProperty("id",out var actor) && actor.ValueKind==JsonValueKind.String &&
                Guid.TryParse(actor.GetString(),out var id)?id:Guid.Empty;
            Dock=DockStyle.Fill;BackColor=Theme.Background;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,124));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            Controls.Add(root);
            var heading=new Panel{Dock=DockStyle.Fill,Padding=new Padding(19,15,18,8),
                BackgroundImage=Theme.LoadImage("mu_banner_institutional.png"),BackgroundImageLayout=ImageLayout.Stretch};
            var headingText=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,BackColor=Color.Transparent};
            headingText.RowStyles.Add(new RowStyle(SizeType.Absolute,55));
            headingText.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            headingText.Controls.Add(Theme.Label("Mensajes internos",27,true),0,0);
            headingText.Controls.Add(Theme.Label("Comunicación privada entre personal activo de Gestión · sin vinculación automática a casos",10),0,1);
            heading.Controls.Add(headingText);root.Controls.Add(heading,0,0);

            var split=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,
                Padding=new Padding(4,3,4,4),Margin=Padding.Empty};
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));
            root.Controls.Add(split,0,1);

            var left=Theme.Card();left.Padding=new Padding(13);split.Controls.Add(left,0,0);
            var leftRows=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=4,ColumnCount=1};
            leftRows.RowStyles.Add(new RowStyle(SizeType.Absolute,39));
            leftRows.RowStyles.Add(new RowStyle(SizeType.Absolute,43));
            leftRows.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            leftRows.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
            left.Controls.Add(leftRows);
            leftRows.Controls.Add(Theme.Label("Personal y conversaciones",16,true),0,0);
            _search=Theme.Input("Buscar personal...");
            _search.TextChanged+=(_,_)=>FilterColleagues();
            leftRows.Controls.Add(_search,0,1);
            var tabs=new TabControl{Dock=DockStyle.Fill,Font=Theme.Text(10,true)};
            var messagesTab=new TabPage("Conversaciones"){BackColor=Color.White};
            var peopleTab=new TabPage("Personal"){BackColor=Color.White};
            _inbox=CreateList();_colleagues=CreateList();
            _inbox.Columns.Add("Nombre",170,HorizontalAlignment.Left);
            _inbox.Columns.Add("Última actividad",115,HorizontalAlignment.Left);
            _colleagues.Columns.Add("Nombre",175,HorizontalAlignment.Left);
            _colleagues.Columns.Add("Rol",130,HorizontalAlignment.Left);
            messagesTab.Controls.Add(_inbox);peopleTab.Controls.Add(_colleagues);
            tabs.TabPages.Add(messagesTab);tabs.TabPages.Add(peopleTab);
            leftRows.Controls.Add(tabs,0,2);
            _inbox.SelectedIndexChanged+=async (_,_)=>await OpenSelectedConversationAsync();
            _colleagues.ItemActivate+=async (_,_)=>await StartSelectedAsync();
            var reload=Theme.Button("↻ Actualizar personal y mensajes",true);
            reload.Click+=async (_,_)=>await ReloadAsync();
            leftRows.Controls.Add(reload,0,3);

            var right=Theme.Card();right.Padding=new Padding(13);split.Controls.Add(right,1,0);
            var rightRows=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=4,ColumnCount=1};
            rightRows.RowStyles.Add(new RowStyle(SizeType.Absolute,75));
            rightRows.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            rightRows.RowStyles.Add(new RowStyle(SizeType.Absolute,90));
            rightRows.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
            right.Controls.Add(rightRows);
            var conversationHeader=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1};
            conversationHeader.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
            conversationHeader.RowStyles.Add(new RowStyle(SizeType.Absolute,33));
            _caption=Theme.Label("Selecciona una conversación",19,true);
            conversationHeader.Controls.Add(_caption,0,0);
            conversationHeader.Controls.Add(Theme.Label("Solo personal de Gestión. No es un canal de atención a usuarias.",10,false,Theme.Muted),0,1);
            rightRows.Controls.Add(conversationHeader,0,0);
            _timeline=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,
                WrapContents=false,BackColor=Color.FromArgb(250,243,254),Padding=new Padding(10)};
            _timeline.Resize+=(_,_)=>ResizeBubbles();
            rightRows.Controls.Add(_timeline,0,1);
            AddNotice("Elige una colega desde «Personal» (doble clic) o abre una conversación existente.");
            var composer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(3,7,3,7)};
            composer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            composer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));
            _draft=new TextBox{Dock=DockStyle.Fill,Multiline=true,MaxLength=2000,
                Font=Theme.Text(11),BorderStyle=BorderStyle.FixedSingle,
                PlaceholderText="Escribe un mensaje al personal autorizado..."};
            _draft.KeyDown+=async (_,e)=>
            {
                if(e.KeyCode==Keys.Enter && e.Control){e.SuppressKeyPress=true;await SendAsync();}
            };
            _send=Theme.Button("➤ Enviar");_send.Enabled=false;
            _send.Click+=async (_,_)=>await SendAsync();
            composer.Controls.Add(_draft,0,0);composer.Controls.Add(_send,1,0);
            rightRows.Controls.Add(composer,0,2);
            _status=Theme.Label("Sin conexión inicializada. Los mensajes requieren Server local y SQL.",10,false,Theme.Muted);
            rightRows.Controls.Add(_status,0,3);
            Load+=async (_,_)=>await ReloadAsync();
        }
        private static ListView CreateList()=>new()
        {
            Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,HideSelection=false,
            MultiSelect=false,GridLines=false,BorderStyle=BorderStyle.None,
            BackColor=Color.White,ForeColor=Theme.Plum,Font=Theme.Text(10),
            HeaderStyle=ColumnHeaderStyle.Nonclickable
        };
        #endregion Initialization
        #region Arrays
        private readonly List<StaffPerson> _people=new();
        #endregion Arrays
        #region Structs
        private readonly record struct StaffPerson(Guid Id,string Name,string Role);
        #endregion Structs
        #region Task
        /// <summary>Refreshes the two lists without exposing community accounts in institutional search.</summary>
        private async Task ReloadAsync()
        {
            if(_busy)return;
            _busy=true;_status.Text="Actualizando...";
            try
            {
                var people=await _api.SendAsync(HttpMethod.Get,"api/management/messaging/staff");
                var chats=await _api.SendAsync(HttpMethod.Get,"api/management/messaging/conversations");
                if(!people.Success||!chats.Success)
                {
                    _status.Text=people.Success?chats.Error:people.Error;return;
                }
                _people.Clear();
                if(people.Data.ValueKind==JsonValueKind.Array)
                    foreach(var item in people.Data.EnumerateArray())
                    {
                        _people.Add(new StaffPerson(item.GetProperty("id").GetGuid(),
                            item.GetProperty("displayName").GetString()??"Personal",
                            item.TryGetProperty("role",out var role)&&role.ValueKind==JsonValueKind.String?
                                role.GetString()??"Staff":"Owner"));
                    }
                FilterColleagues();_inbox.BeginUpdate();_inbox.Items.Clear();
                if(chats.Data.ValueKind==JsonValueKind.Array)
                    foreach(var item in chats.Data.EnumerateArray())
                    {
                        var conversationId=item.GetProperty("id").GetGuid();
                        var row=new ListViewItem(item.GetProperty("otherUserName").GetString()??"Colega")
                            {Tag=conversationId};
                        row.SubItems.Add(item.GetProperty("updatedUtc").GetDateTime().ToLocalTime().ToString("dd/MM HH:mm"));
                        _inbox.Items.Add(row);
                    }
                _inbox.EndUpdate();
                _status.Text="Mensajes internos · actualizado";
            }
            catch(Exception ex) when(ex is JsonException or InvalidOperationException)
            {
                _status.Text="Respuesta de Server inesperada. Revisa logs y versión de migración 080.";
            }
            finally{_busy=false;}
        }
        private void FilterColleagues()
        {
            if(_colleagues is null)return;
            string term=_search.Text.Trim();
            _colleagues.BeginUpdate();_colleagues.Items.Clear();
            foreach(var person in _people.Where(x=>x.Name.Contains(term,StringComparison.OrdinalIgnoreCase)||
                x.Role.Contains(term,StringComparison.OrdinalIgnoreCase)))
            {
                var row=new ListViewItem(person.Name){Tag=person.Id};
                row.SubItems.Add(person.Role);_colleagues.Items.Add(row);
            }
            _colleagues.EndUpdate();
        }
        private async Task StartSelectedAsync()
        {
            if(_colleagues.SelectedItems.Count!=1||_busy)return;
            if(_colleagues.SelectedItems[0].Tag is not Guid target)return;
            var reply=await _api.SendAsync(HttpMethod.Post,"api/management/messaging/conversations",
                new {recipientId=target});
            if(!reply.Success){_status.Text=reply.Error;return;}
            if(reply.Data.TryGetProperty("conversationId",out var id))
            {
                _conversationId=id.GetGuid();_caption.Text=_colleagues.SelectedItems[0].Text;
                _send.Enabled=true;await LoadMessagesAsync();await ReloadAsync();
            }
        }
        private async Task OpenSelectedConversationAsync()
        {
            if(_inbox.SelectedItems.Count!=1||_busy)return;
            if(_inbox.SelectedItems[0].Tag is not Guid id)return;
            _conversationId=id;_caption.Text=_inbox.SelectedItems[0].Text;
            _send.Enabled=true;await LoadMessagesAsync();
        }
        private async Task LoadMessagesAsync()
        {
            if(_conversationId==Guid.Empty)return;
            var result=await _api.SendAsync(HttpMethod.Get,$"api/management/messaging/conversations/{_conversationId}/messages");
            _timeline.SuspendLayout();_timeline.Controls.Clear();
            if(!result.Success){AddNotice(result.Error);_status.Text=result.Error;_send.Enabled=false;}
            else if(result.Data.ValueKind!=JsonValueKind.Array){AddNotice("Respuesta inesperada de Server.");}
            else
            {
                foreach(var item in result.Data.EnumerateArray())
                {
                    var mine=item.GetProperty("senderId").GetGuid()==_actorId;
                    var title=mine?"Tú":item.GetProperty("senderName").GetString()??"Personal";
                    AddBubble(title,item.GetProperty("body").GetString()??"",
                        item.GetProperty("createdUtc").GetDateTime().ToLocalTime(),mine);
                }
                if(result.Data.GetArrayLength()==0)AddNotice("Aún no hay mensajes. Inicia una conversación con respeto.");
            }
            _timeline.ResumeLayout();
            if(_timeline.Controls.Count>0)_timeline.ScrollControlIntoView(_timeline.Controls[^1]);
        }
        private async Task SendAsync()
        {
            var text=_draft.Text.Trim();
            if(_conversationId==Guid.Empty||text.Length is <1 or >2000)return;
            _send.Enabled=false;
            var result=await _api.SendAsync(HttpMethod.Post,
                $"api/management/messaging/conversations/{_conversationId}/messages",new {body=text});
            if(result.Success){_draft.Clear();await LoadMessagesAsync();await ReloadAsync();}
            else _status.Text=result.Error;
            _send.Enabled=true;
        }
        private void AddBubble(string sender,string body,DateTime when,bool mine)
        {
            int width=Math.Max(200,_timeline.ClientSize.Width-45);
            var background=mine?Theme.Lavender:Color.White;
            var label=new Label{Text=sender+"  ·  "+when.ToString("HH:mm")+Environment.NewLine+body,
                Font=Theme.Text(11),ForeColor=Theme.Plum,BackColor=background,
                TextAlign=ContentAlignment.MiddleLeft,AutoSize=false,Padding=new Padding(15,9,12,9),
                MaximumSize=new Size(width,0),Dock=DockStyle.Fill};
            var measurement=TextRenderer.MeasureText(label.Text,label.Font,new Size(width-30,int.MaxValue),
                TextFormatFlags.WordBreak);
            var container=new Panel{BackColor=background,Width=width,
                Height=Math.Max(58,measurement.Height+26),Margin=new Padding(3,5,3,5),Padding=new Padding(3)};
            container.Controls.Add(label);_timeline.Controls.Add(container);
        }
        private void AddNotice(string message)
        {
            var line=new Label{Text=message,ForeColor=Theme.Muted,Font=Theme.Text(10),
                Width=Math.Max(200,_timeline.ClientSize.Width-45),Height=64,
                Padding=new Padding(15),BackColor=Color.White};
            _timeline.Controls.Add(line);
        }
        private void ResizeBubbles()
        {
            int width=Math.Max(200,_timeline.ClientSize.Width-45);
            foreach(Control control in _timeline.Controls)control.Width=width;
        }
        #endregion Task
    }
}
