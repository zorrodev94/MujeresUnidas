using System.Data;
using Microsoft.Data.SqlClient;
using MujeresUnidas.Server.Infrastructure.Database;
using MujeresUnidas.Server.Models.User.Profiles;

namespace MujeresUnidas.Server.Models.Safety
{
    /// <summary>Owner-scoped emergency contacts; phone numbers are stored as AES-GCM ciphertext, never plaintext.</summary>
    public sealed class EmergencyContactRepository(Database database, EmergencyContactSecurity security)
    {
        #region DTO
        public sealed record ContactData(Guid Id, string Name, string Relationship, string Phone, int Priority);
        public sealed record CreateContactData(string Name, string Relationship, string Phone);
        #endregion

        #region Queries
        public bool IsConfigured => security.IsConfigured;

        public async Task<IReadOnlyList<ContactData>> AllAsync(Guid ownerId)
        {
            var contacts = new List<ContactData>();
            await using var db = await database.OpenAsync();
            await using var cmd = new SqlCommand(@"SELECT Id,DisplayName,Relationship,PhoneCipher,Priority
FROM dbo.EmergencyContacts WHERE OwnerId=@owner ORDER BY Priority,CreatedUtc", db);
            cmd.Parameters.Add("@owner", SqlDbType.UniqueIdentifier).Value = ownerId;
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                contacts.Add(new ContactData(reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                    security.Decrypt((byte[])reader.GetValue(3)), reader.GetInt32(4)));
            return contacts;
        }

        public async Task<(bool Saved, string Message)> AddAsync(Guid ownerId, CreateContactData data)
        {
            string name = (data.Name ?? string.Empty).Trim();
            string relation = (data.Relationship ?? string.Empty).Trim();
            string phone = (data.Phone ?? string.Empty).Trim();
            string digits = new(phone.Where(char.IsDigit).ToArray());
            if (name.Length is < 2 or > 70 || relation.Length is < 2 or > 35 ||
                digits.Length is < 7 or > 15 ||
                phone.Any(c => !char.IsDigit(c) && c != '+' && c != ' ' && c != '-' && c != '(' && c != ')') ||
                (phone.Count(c => c == '+') > 1) || (phone.Contains('+') && !phone.StartsWith('+')))
                return (false, "Revisa el nombre, parentesco y número telefónico.");
            await using var db = await database.OpenAsync();
            await using var transaction = (SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable);
            await using (var count = new SqlCommand(
                "SELECT COUNT(1) FROM dbo.EmergencyContacts WITH (UPDLOCK,HOLDLOCK) WHERE OwnerId=@owner",
                db, transaction))
            {
                count.Parameters.Add("@owner", SqlDbType.UniqueIdentifier).Value = ownerId;
                if (Convert.ToInt32(await count.ExecuteScalarAsync()) >= 3)
                    return (false, "Máximo tres contactos por cuenta.");
            }
            await using (var insert = new SqlCommand(@"INSERT dbo.EmergencyContacts
(Id,OwnerId,DisplayName,Relationship,PhoneCipher,Priority)
VALUES(NEWID(),@owner,@name,@relationship,@phone,1)", db, transaction))
            {
                insert.Parameters.Add("@owner", SqlDbType.UniqueIdentifier).Value = ownerId;
                insert.Parameters.Add("@name", SqlDbType.NVarChar, 70).Value = name;
                insert.Parameters.Add("@relationship", SqlDbType.NVarChar, 35).Value = relation;
                insert.Parameters.Add("@phone", SqlDbType.VarBinary, 256).Value = security.Encrypt(phone);
                await insert.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
            return (true, "Contacto de confianza guardado.");
        }

        public async Task<bool> DeleteAsync(Guid ownerId, Guid contactId)
        {
            await using var db = await database.OpenAsync();
            await using var cmd = new SqlCommand("DELETE dbo.EmergencyContacts WHERE Id=@id AND OwnerId=@owner", db);
            cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = contactId;
            cmd.Parameters.Add("@owner", SqlDbType.UniqueIdentifier).Value = ownerId;
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        #endregion
    }
}
