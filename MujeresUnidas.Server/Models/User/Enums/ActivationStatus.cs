namespace MujeresUnidas.Server.Models.User.Enums
{
    /// <summary>Tracks the review stage, independently from the account's access state.</summary>
    public enum ActivationStatus : byte
    {
        AwaitingAppointment = 0,
        Approved = 1,
        Declined = 2
    }
}
