using BaseUser = global::MujeresUnidas.Gestion.Models.User.Base.User;

namespace MujeresUnidas.Gestion.Models.User.Contracts
{
    /// <summary>Type-safe account login and registration operations for a client.</summary>
    public interface IAccountAccess<TRegistration>
    {
        Task<BaseUser.OperationResultData> LoginAsync(BaseUser.LoginData data);
        Task<BaseUser.OperationResultData> RegisterAsync(TRegistration data);
    }
}
