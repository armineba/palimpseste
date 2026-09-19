using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Palimpseste.Api;

// This is a one-time invitation exchange. It never accepts a Codex credential
// and never grants access to the worker or its filesystem.
public static class PlayerSession
{
    public static async Task<IResult> Redeem(HttpContext context, NpgsqlDataSource db, CancellationToken ct)
    {
        if (!context.Request.HasJsonContentType() || context.Request.ContentLength > 1024)
            return ApiProblem.Result(context, 400, "invalid_request", "Invitation invalide.");

        string? invitationCode;
        try
        {
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, false, leaveOpen: true);
            var buffer = new char[1025];
            var count = await reader.ReadBlockAsync(buffer.AsMemory(), ct);
            if (count > 1024) return ApiProblem.Result(context, 400, "invalid_request", "Invitation invalide.");
            using var document = JsonDocument.Parse(new string(buffer, 0, count),
                new JsonDocumentOptions { MaxDepth = 4 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return ApiProblem.Result(context, 400, "invalid_request", "Invitation invalide.");
            var properties = document.RootElement.EnumerateObject().ToArray();
            if (properties.Length != 1 || properties[0].Name != "invitation_code" ||
                properties[0].Value.ValueKind != JsonValueKind.String)
                return ApiProblem.Result(context, 400, "invalid_request", "Invitation invalide.");
            invitationCode = properties[0].Value.GetString();
        }
        catch (JsonException)
        {
            return ApiProblem.Result(context, 400, "invalid_request", "Invitation invalide.");
        }
        if (invitationCode is null || invitationCode.Length != 64 ||
            invitationCode.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
            return ApiProblem.Result(context, 400, "invalid_request", "Invitation invalide.");

        var invitationHash = SHA256.HashData(Encoding.UTF8.GetBytes(invitationCode));
        try
        {
            await using var connection = await db.OpenConnectionAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            Guid invitationId;
            string label;
            await using (var command = new NpgsqlCommand(
                "SELECT id,label FROM lab_invitations WHERE invitation_sha256=@hash AND redeemed_at IS NULL AND expires_at>now() FOR UPDATE",
                connection, transaction))
            {
                command.Parameters.AddWithValue("hash", invitationHash);
                await using var row = await command.ExecuteReaderAsync(ct);
                if (!await row.ReadAsync(ct))
                    return ApiProblem.Result(context, 401, "invitation_invalid", "Invitation expirée ou déjà utilisée.");
                invitationId = row.GetGuid(0);
                label = row.GetString(1);
            }

            var principalId = Guid.NewGuid();
            var tokenId = Guid.NewGuid();
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            await using (var command = new NpgsqlCommand(
                "INSERT INTO lab_principals(id,role,label) VALUES (@id,'player',@label)",
                connection, transaction))
            {
                command.Parameters.AddWithValue("id", principalId);
                command.Parameters.AddWithValue("label", label);
                await command.ExecuteNonQueryAsync(ct);
            }
            await using (var command = new NpgsqlCommand(
                "INSERT INTO lab_tokens(id,principal_id,token_sha256) VALUES (@id,@principal,@hash)",
                connection, transaction))
            {
                command.Parameters.AddWithValue("id", tokenId);
                command.Parameters.AddWithValue("principal", principalId);
                command.Parameters.AddWithValue("hash", tokenHash);
                await command.ExecuteNonQueryAsync(ct);
            }
            await using (var command = new NpgsqlCommand(
                "UPDATE lab_invitations SET redeemed_at=now(),principal_id=@principal,token_id=@token WHERE id=@id",
                connection, transaction))
            {
                command.Parameters.AddWithValue("id", invitationId);
                command.Parameters.AddWithValue("principal", principalId);
                command.Parameters.AddWithValue("token", tokenId);
                await command.ExecuteNonQueryAsync(ct);
            }
            await transaction.CommitAsync(ct);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Json(new { token, principal_id = principalId.ToString("N") });
        }
        catch (NpgsqlException)
        {
            return ApiProblem.Result(context, 503, "database_unavailable", "Laboratoire temporairement indisponible.", true);
        }
    }
}
