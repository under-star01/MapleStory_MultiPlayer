using System;

public class UserRecord
{
    public int UserId { get; }
    public string LoginId { get; }
    public string PasswordHash { get; }
    public string Nickname { get; }
    public string LastMapId { get; }
    public string LastSpawnId { get; }
    public DateTime CreatedAt { get; }
    public DateTime UpdatedAt { get; }

    public UserRecord(
        int userId,
        string loginId,
        string passwordHash,
        string nickname,
        string lastMapId,
        string lastSpawnId,
        DateTime createdAt,
        DateTime updatedAt)
    {
        UserId = userId;
        LoginId = loginId;
        PasswordHash = passwordHash;
        Nickname = nickname;
        LastMapId = lastMapId;
        LastSpawnId = lastSpawnId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }
}