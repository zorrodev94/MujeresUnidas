using BaseUser = global::MujeresUnidas.Comunidad.Models.User.Base.User;

namespace MujeresUnidas.Comunidad.Models.User.Contracts
{
    /// <summary>Defines login and a type-specific account registration request.</summary>
    public interface IAccountAccess<TRegistration>
    {
        Task<BaseUser.OperationResultData> LoginAsync(BaseUser.LoginData data);
        Task<BaseUser.OperationResultData> RegisterAsync(TRegistration data);
    }
}
