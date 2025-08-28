using GrpcApp;

namespace Server_Study.Modules.Auth.Register;

public class UserRegister
{
    private static readonly HashSet<string> _registeredUsers = new()
    {
        "testuser", "admin"
    };

    public static GameMessage ProcessRegister(AuthUser authUser)
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

        if (authUser.AuthKey.Length < 3)
        {
            response.ResultCode = (int)ResultCode.Fail;
            response.ResultMessage = "Username must be at least 3 characters";
            return response;
        }

        if (authUser.RetPassKey.Length < 6)
        {
            response.ResultCode = (int)ResultCode.Fail;
            response.ResultMessage = "Password must be at least 6 characters";
            return response;
        }

        if (_registeredUsers.Contains(authUser.AuthKey))
        {
            response.ResultCode = (int)ResultCode.Fail;
            response.ResultMessage = "Username already exists";
            return response;
        }

        _registeredUsers.Add(authUser.AuthKey);
        response.ResultCode = (int)ResultCode.Success;
        response.ResultMessage = "Registration successful";
        response.UserId = authUser.AuthKey;
        response.Token = GenerateToken(authUser.AuthKey);

        return response;
    }

    private static string GenerateToken(string userId)
    {
        return $"token_{userId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    }
}