using NewAGV.Contracts;

namespace NewAGV.Api.Infrastructure;

public static class RequestRoleExtensions
{
    public static UserRole ResolveRole(this HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Demo-Role", out var values) &&
            Enum.TryParse<UserRole>(values.ToString(), true, out var role))
        {
            return role;
        }

        return UserRole.Operator;
    }
}
