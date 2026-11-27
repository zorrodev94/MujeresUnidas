namespace MujeresUnidas.Shared.Contracts;
public sealed record ConversationDto(Guid Id,string Title,string Type,DateTime CreatedUtc);
public sealed record MessageDto(Guid Id,Guid ConversationId,Guid SenderId,string Body,DateTime CreatedUtc);
public sealed record MessageDraftDto(string Body);
public enum MessagingContext { Community, Management }
