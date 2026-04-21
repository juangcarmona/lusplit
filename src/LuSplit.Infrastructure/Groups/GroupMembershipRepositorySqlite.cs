using LuSplit.Application.Groups.Ports;
using LuSplit.Domain.Groups;
using LuSplit.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace LuSplit.Infrastructure.Groups;

public sealed class GroupMembershipRepositorySqlite : IGroupMembershipRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransactionRunner _transactionRunner;

    public GroupMembershipRepositorySqlite(SqliteConnection connection, SqliteTransactionRunner transactionRunner)
    {
        _connection = connection;
        _transactionRunner = transactionRunner;
    }

    public Task<IReadOnlyList<GroupMembership>> GetByGroupIdAsync(string groupId, CancellationToken ct = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
SELECT user_id, role, joined_at, is_revoked, revoked_at
FROM group_memberships
WHERE group_id = $groupId AND is_revoked = 0";
        cmd.Parameters.AddWithValue("$groupId", groupId);

        using var reader = cmd.ExecuteReader();
        var results = new List<GroupMembership>();
        while (reader.Read())
        {
            results.Add(new GroupMembership(
                GroupId: groupId,
                UserId: reader.GetString(0),
                Role: Enum.Parse<MemberRole>(reader.GetString(1)),
                JoinedAt: DateTimeOffset.Parse(reader.GetString(2)),
                IsRevoked: reader.GetInt32(3) != 0,
                RevokedAt: reader.IsDBNull(4) ? null : DateTimeOffset.Parse(reader.GetString(4))));
        }
        return Task.FromResult<IReadOnlyList<GroupMembership>>(results);
    }

    public Task UpsertAsync(GroupMembership membership, CancellationToken ct = default)
    {
        return _transactionRunner.RunInTransactionAsync(async () =>
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO group_memberships (group_id, user_id, role, joined_at, is_revoked, revoked_at)
VALUES ($groupId, $userId, $role, $joinedAt, $isRevoked, $revokedAt)
ON CONFLICT(group_id, user_id) DO UPDATE SET
    role = excluded.role,
    is_revoked = excluded.is_revoked,
    revoked_at = excluded.revoked_at";
            cmd.Parameters.AddWithValue("$groupId", membership.GroupId);
            cmd.Parameters.AddWithValue("$userId", membership.UserId);
            cmd.Parameters.AddWithValue("$role", membership.Role.ToString());
            cmd.Parameters.AddWithValue("$joinedAt", membership.JoinedAt.ToString("O"));
            cmd.Parameters.AddWithValue("$isRevoked", membership.IsRevoked ? 1 : 0);
            cmd.Parameters.AddWithValue("$revokedAt", membership.RevokedAt.HasValue ? (object)membership.RevokedAt.Value.ToString("O") : DBNull.Value);
            cmd.ExecuteNonQuery();
            await Task.CompletedTask;
        });
    }
}
