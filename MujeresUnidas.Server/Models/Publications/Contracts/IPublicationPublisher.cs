namespace MujeresUnidas.Server.Models.Publications.Contracts;

public interface IPublicationPublisher<TDraft>
{
    Task<Guid?> PublishAsync(Guid actorId,TDraft draft);
    Task<bool> WithdrawOwnAsync(Guid actorId,Guid publicationId);
}
