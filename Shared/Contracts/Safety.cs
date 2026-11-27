namespace MujeresUnidas.Shared.Contracts;
public sealed record EmergencyContactDto(Guid Id,string Name,string Relationship,string PhoneNumber);
public sealed record IncidentReportDto(Guid Id,string Category,string Status,DateTime CreatedUtc);
public sealed record OfficialSupportLineDto(string Name,string Telephone,string Description);
