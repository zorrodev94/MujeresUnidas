namespace MujeresUnidas.Server.Models.Messages.Contracts;

/// <summary>Capability contract: actual access checks require account, application, conversation and message.</summary>
public interface IMessageParticipant
{
    Task<bool> CanParticipateAsync(Guid accountId,Guid conversationId,string applicationCode);
}
