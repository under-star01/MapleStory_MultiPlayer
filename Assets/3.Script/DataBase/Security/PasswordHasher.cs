using System;
using System.Security.Cryptography;

public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    private const char Separator = ':';

    /// <summary>
    /// 입력받은 비밀번호를 PBKDF2로 해시합니다.
    ///
    /// 저장 형식:
    /// 반복 횟수:Salt:Hash
    /// </summary>
    public static string HashPassword(
        string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException(
                "비밀번호가 비어 있습니다.",
                nameof(password)
            );
        }

        byte[] salt = new byte[SaltSize];

        using (RandomNumberGenerator random =
               RandomNumberGenerator.Create())
        {
            random.GetBytes(salt);
        }

        byte[] hash;

        using (Rfc2898DeriveBytes deriveBytes =
               new Rfc2898DeriveBytes(
                   password,
                   salt,
                   Iterations,
                   HashAlgorithmName.SHA256
               ))
        {
            hash = deriveBytes.GetBytes(
                HashSize
            );
        }

        return string.Join(
            Separator,
            Iterations.ToString(),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash)
        );
    }

    /// <summary>
    /// 입력한 비밀번호가 저장된 해시와 일치하는지 확인합니다.
    /// </summary>
    public static bool VerifyPassword(
        string password,
        string storedPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(storedPasswordHash))
        {
            return false;
        }

        string[] parts =
            storedPasswordHash.Split(
                Separator
            );

        if (parts.Length != 3)
            return false;

        if (!int.TryParse(
                parts[0],
                out int iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] storedHash;

        try
        {
            salt = Convert.FromBase64String(
                parts[1]
            );

            storedHash = Convert.FromBase64String(
                parts[2]
            );
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] inputHash;

        using (Rfc2898DeriveBytes deriveBytes =
               new Rfc2898DeriveBytes(
                   password,
                   salt,
                   iterations,
                   HashAlgorithmName.SHA256
               ))
        {
            inputHash = deriveBytes.GetBytes(
                storedHash.Length
            );
        }

        return FixedTimeEquals(
            inputHash,
            storedHash
        );
    }

    /// <summary>
    /// 비교 도중 어느 위치에서 값이 달랐는지가
    /// 실행 시간에 드러나지 않도록 전체 바이트를 비교합니다.
    /// </summary>
    private static bool FixedTimeEquals(
        byte[] left,
        byte[] right)
    {
        if (left == null ||
            right == null ||
            left.Length != right.Length)
        {
            return false;
        }

        int difference = 0;

        for (int i = 0;
             i < left.Length;
             i++)
        {
            difference |=
                left[i] ^ right[i];
        }

        return difference == 0;
    }
}