namespace MujeresUnidas.Gestion.Models.Media;
public sealed class ManagementMediaPolicy
{
    public const long DefaultAccountQuotaBytes=50L*1024*1024;
    public bool IsWithinClientHint(long bytes)=>bytes>0 && bytes<=DefaultAccountQuotaBytes;
}
