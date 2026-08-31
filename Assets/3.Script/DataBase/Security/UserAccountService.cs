using System;
using System.Threading.Tasks;
using MySqlConnector;

public enum RegisterResult
{
    Success,
    InvalidLoginId,
    InvalidPassword,
    InvalidNickname,
    LoginIdAlreadyExists,
    NicknameAlreadyExists,
    DatabaseError
}

public class LoginServiceResult
{
    public LoginResult Result { get; }
    public UserRecord User { get; }

    public LoginServiceResult(
        LoginResult result,
        UserRecord user = null)
    {
        Result = result;
        User = user;
    }
}

public class UserAccountService
{
    private readonly UserRepository userRepository;

    public UserAccountService(
        UserRepository userRepository)
    {
        this.userRepository =
            userRepository ??
            throw new ArgumentNullException(
                nameof(userRepository)
            );
    }

    /// <summary>
    /// 회원가입 입력값을 검증하고
    /// 신규 유저를 생성합니다.
    /// </summary>
    public async Task<RegisterResult>
        RegisterAsync(
            string loginId,
            string password,
            string nickname)
    {
        loginId =
            loginId?.Trim();

        nickname =
            nickname?.Trim();

        if (!IsValidLoginId(loginId))
        {
            return RegisterResult.InvalidLoginId;
        }

        if (!IsValidPassword(password))
        {
            return RegisterResult.InvalidPassword;
        }

        if (!IsValidNickname(nickname))
        {
            return RegisterResult.InvalidNickname;
        }

        try
        {
            if (await userRepository
                    .IsLoginIdTakenAsync(loginId))
            {
                return RegisterResult
                    .LoginIdAlreadyExists;
            }

            if (await userRepository
                    .IsNicknameTakenAsync(nickname))
            {
                return RegisterResult
                    .NicknameAlreadyExists;
            }

            string passwordHash =
                PasswordHasher.HashPassword(
                    password
                );

            await userRepository.CreateUserAsync(
                loginId,
                passwordHash,
                nickname
            );

            return RegisterResult.Success;
        }
        catch (MySqlException exception)
            when (exception.Number == 1062)
        {
            if (exception.Message.Contains(
                    "uq_users_login_id"))
            {
                return RegisterResult
                    .LoginIdAlreadyExists;
            }

            if (exception.Message.Contains(
                    "uq_users_nickname"))
            {
                return RegisterResult
                    .NicknameAlreadyExists;
            }

            return RegisterResult.DatabaseError;
        }
        catch (Exception)
        {
            return RegisterResult.DatabaseError;
        }
    }

    private static bool IsValidLoginId(
        string loginId)
    {
        if (string.IsNullOrWhiteSpace(loginId))
            return false;

        return loginId.Length >= 4 &&
               loginId.Length <= 30;
    }

    private static bool IsValidPassword(
        string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;

        return password.Length >= 8 &&
               password.Length <= 50;
    }

    private static bool IsValidNickname(
        string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return false;

        return nickname.Length >= 2 &&
               nickname.Length <= 20;
    }

    public async Task<LoginServiceResult> LoginAsync(
        string loginId,
        string password)
    {
        loginId =
            loginId?.Trim();

        if (string.IsNullOrWhiteSpace(loginId) ||
            string.IsNullOrWhiteSpace(password))
        {
            return new LoginServiceResult(
                LoginResult.InvalidInput
            );
        }

        try
        {
            UserRecord user =
                await userRepository
                    .FindByLoginIdAsync(
                        loginId
                    );

            if (user == null)
            {
                return new LoginServiceResult(
                    LoginResult.UserNotFound
                );
            }

            if (!PasswordHasher.VerifyPassword(
                    password,
                    user.PasswordHash))
            {
                return new LoginServiceResult(
                    LoginResult.IncorrectPassword
                );
            }

            return new LoginServiceResult(
                LoginResult.Success,
                user
            );
        }
        catch (Exception)
        {
            return new LoginServiceResult(
                LoginResult.DatabaseError
            );
        }
    }
}