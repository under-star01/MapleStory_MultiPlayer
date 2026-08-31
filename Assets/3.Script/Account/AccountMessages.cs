using Mirror;

public enum LoginResult
{
    Success,
    InvalidInput,
    UserNotFound,
    IncorrectPassword,
    AlreadyLoggedIn,
    DatabaseError
}

public struct RegisterRequestMessage
    : NetworkMessage
{
    public string LoginId;
    public string Password;
    public string Nickname;
}

public struct RegisterResponseMessage
    : NetworkMessage
{
    public RegisterResult Result;
}

public struct LoginRequestMessage
    : NetworkMessage
{
    public string LoginId;
    public string Password;
}

public struct LoginResponseMessage
    : NetworkMessage
{
    public LoginResult Result;
    public string Nickname;
}