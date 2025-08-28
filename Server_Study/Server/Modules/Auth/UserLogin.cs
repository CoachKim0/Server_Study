using GrpcApp;

namespace Server_Study.Modules.Auth.Login;

public class UserLogin
{
    private static readonly Dictionary<string, string> _users = new()
    {
        { "testuser", "password123" },
        { "admin", "admin123" }
    };

    public static GameMessage ProcessLogin(AuthUser authUser)
    {
        var response = new GameMessage
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            AuthUser = authUser
        };

        if (string.IsNullOrEmpty(authUser.AuthKey))
        {
            response.ResultCode = (int)ResultCode.Fail;
            response.ResultMessage = "Username is required";
            return response;
        }

        if (string.IsNullOrEmpty(authUser.RetPassKey))
        {
            response.ResultCode = (int)ResultCode.Fail;
            response.ResultMessage = "Password is required";
            return response;
        }

        if (_users.ContainsKey(authUser.AuthKey) && _users[authUser.AuthKey] == authUser.RetPassKey)
        {
            response.ResultCode = (int)ResultCode.Success;
            response.ResultMessage = "Login successful";
            response.UserId = authUser.AuthKey;
            response.Token = GenerateToken(authUser.AuthKey);
        }
        else
        {
            response.ResultCode = (int)ResultCode.AuthenticationFailed;
            response.ResultMessage = "Invalid username or password";
        }

        return response;
    }

    private static string GenerateToken(string userId)
    {
        return $"token_{userId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    }
}