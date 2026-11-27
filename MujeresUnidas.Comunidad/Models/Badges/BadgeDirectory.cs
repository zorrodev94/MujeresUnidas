using System.Text.Json;
using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Presentation;

namespace MujeresUnidas.Comunidad.Models.Badges;

/// <summary>Read-only presentation of granted achievements; a badge never confers a role.</summary>
public sealed record AwardedBadge(Guid BadgeId,string Name,string Description,string Scope,DateTime AwardedUtc);

public sealed class BadgeDirectory(ApiClient api)
{
    public async Task<(bool Success, IReadOnlyList<AwardedBadge> Items, string Error)> ForUserAsync(Guid userId)
    {
        if (userId == Guid.Empty) return (false, Array.Empty<AwardedBadge>(), "Cuenta no identificada.");
        var reply = await api.RequestAsync(HttpMethod.Get, $"api/badges/{userId}");
        if (!reply.Success) return (false, Array.Empty<AwardedBadge>(), reply.Error);
        try
        {
            var awards = new List<AwardedBadge>();
            foreach(var item in reply.Data.EnumerateArray())
            {
                var scope = Get(item,"scope");
                if (scope is not ("Global" or "Community")) continue;
                awards.Add(new AwardedBadge(item.GetProperty("badgeId").GetGuid(),Get(item,"name"),
                    Get(item,"description"),scope,
                    item.TryGetProperty("awardedUtc",out var date)&&date.ValueKind==JsonValueKind.String?date.GetDateTime():DateTime.MinValue));
            }
            return (true, awards, string.Empty);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {return (false, Array.Empty<AwardedBadge>(), "El catálogo de reconocimientos no está disponible.");}
    }

    private static string Get(JsonElement data, string name) =>
        data.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.String ? value.GetString()??string.Empty : string.Empty;

    /// <summary>Responsive gallery built with native controls: no external icon resources or fake awards.</summary>
    internal static View Gallery(IReadOnlyList<AwardedBadge> awards)
    {
        var items = new VerticalStackLayout {Spacing=7};
        foreach(var badge in awards)
        {
            var name=badge.Name;
            var icon = name.Contains("Ganadora",StringComparison.OrdinalIgnoreCase)?"badge_ganadora.png":
                       name.Contains("Colabora",StringComparison.OrdinalIgnoreCase)?"badge_colaboracion.png":"badge_participante.png";
            var iconColor = name.Contains("Ganadora",StringComparison.OrdinalIgnoreCase)?Color.FromArgb("#F8DF95"):
                            name.Contains("Colabora",StringComparison.OrdinalIgnoreCase)?Color.FromArgb("#F7DCF1"):Ui.Lavender;
            var line=new Grid{ColumnSpacing=11,ColumnDefinitions={new ColumnDefinition(new GridLength(52)),
                new ColumnDefinition(GridLength.Star)}};
            var emblem=new Border{BackgroundColor=iconColor,Stroke=Ui.BorderColor,StrokeThickness=1,
                StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle{CornerRadius=18},
                WidthRequest=49,HeightRequest=49,
                Content=new Image{Source=icon,WidthRequest=44,HeightRequest=44,Aspect=Aspect.AspectFit,HorizontalOptions=LayoutOptions.Center,VerticalOptions=LayoutOptions.Center}};
            line.Add(emblem,0,0);
            var description=new VerticalStackLayout{Spacing=2,VerticalOptions=LayoutOptions.Center};
            description.Children.Add(Ui.Heading(name,14));
            description.Children.Add(Ui.Text(string.IsNullOrWhiteSpace(badge.Description)?"Reconocimiento otorgado por Gestión":badge.Description,11));
            line.Add(description,1,0);
            items.Children.Add(Ui.Card(line,9));
        }
        return items;
    }
}
