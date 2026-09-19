using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace Palimpseste.Api;

public static class AdminCommands
{
    public static async Task<int> RunAsync(string[] args, ApiConfig config)
    {
        if (args.Length == 2 && args[0] == "invite-create")
        {
            if (args[1].Length is < 1 or > 120) return 2;
            var invitationId = Guid.NewGuid();
            var invitation = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(invitation));
            await using var db = NpgsqlDataSource.Create(config.ConnectionString);
            await using var connection = await db.OpenConnectionAsync();
            await using var command = new NpgsqlCommand(
                "INSERT INTO lab_invitations(id,invitation_sha256,label,expires_at) VALUES (@id,@hash,@label,now() + interval '24 hours')",
                connection);
            command.Parameters.AddWithValue("id", invitationId);
            command.Parameters.AddWithValue("hash", hash);
            command.Parameters.AddWithValue("label", args[1]);
            await command.ExecuteNonQueryAsync();
            Console.WriteLine($"invitation_id={invitationId:N}");
            Console.WriteLine($"invitation_code={invitation}");
            Console.WriteLine("expires_after=24h");
            return 0;
        }
        if (args.Length == 3 && args[0] == "token-create" && args[1] is "player" or "creator")
        {
            if (args[2].Length is < 1 or > 120) return 2;
            var principalId = Guid.NewGuid();
            var tokenId = Guid.NewGuid();
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            await using var db = NpgsqlDataSource.Create(config.ConnectionString);
            await using var connection = await db.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using (var principal = new NpgsqlCommand("INSERT INTO lab_principals(id,role,label) VALUES (@id,@role,@label)", connection, transaction))
            {
                principal.Parameters.AddWithValue("id", principalId);
                principal.Parameters.AddWithValue("role", args[1]);
                principal.Parameters.AddWithValue("label", args[2]);
                await principal.ExecuteNonQueryAsync();
            }
            await using (var credential = new NpgsqlCommand("INSERT INTO lab_tokens(id,principal_id,token_sha256) VALUES (@id,@principal,@hash)", connection, transaction))
            {
                credential.Parameters.AddWithValue("id", tokenId);
                credential.Parameters.AddWithValue("principal", principalId);
                credential.Parameters.AddWithValue("hash", hash);
                await credential.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
            Console.WriteLine($"principal_id={principalId:N}");
            Console.WriteLine($"token_id={tokenId:N}");
            Console.WriteLine($"token={token}");
            return 0;
        }
        if (args.Length == 2 && args[0] == "token-revoke" && Guid.TryParseExact(args[1], "N", out var revokeId))
        {
            await using var db = NpgsqlDataSource.Create(config.ConnectionString);
            await using var connection = await db.OpenConnectionAsync();
            await using var command = new NpgsqlCommand("UPDATE lab_tokens SET revoked_at=now() WHERE id=@id AND revoked_at IS NULL", connection);
            command.Parameters.AddWithValue("id", revokeId);
            var changed = await command.ExecuteNonQueryAsync();
            Console.WriteLine(changed == 1 ? "revoked" : "not_found");
            return changed == 1 ? 0 : 1;
        }
        Console.Error.WriteLine("Usage: invite-create <player-label> | token-create <player|creator> <label> | token-revoke <token-id-N>");
        return 2;
    }
}
