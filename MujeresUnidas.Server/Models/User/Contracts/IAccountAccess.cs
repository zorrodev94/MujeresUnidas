using MujeresUnidas.Server.Contracts;
using BaseUser = global::MujeresUnidas.Server.Models.User.Base.User;

namespace MujeresUnidas.Server.Models.User.Contracts
{
    /// <summary>Defines login and type-safe registration for an account.</summary>
    public interface IAccountAccess<TRegistration>
    {
        Task<(AuthResponse? Response, string Status)> LoginAsync(BaseUser.LoginData data);

        Task<string> RegisterAsync(TRegistration data);
    }
}
