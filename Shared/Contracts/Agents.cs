namespace MujeresUnidas.Shared.Contracts;
/// <summary>Future agent contract only. No Azure calls, medical claims or autonomous mutations.</summary>
public sealed record AssistantCapabilitiesDto(string DisplayName,int MaxMessagesPerUserPerHour,bool Enabled,
    bool CanManageUsers=false,bool CanReadPrivateCases=false,bool CanModerate=false);
