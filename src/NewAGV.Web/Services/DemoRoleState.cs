using NewAGV.Contracts;

namespace NewAGV.Web.Services;

public sealed class DemoRoleState
{
    public UserRole CurrentRole { get; private set; } = UserRole.Operator;

    public event Action? Changed;

    public void SetRole(UserRole role)
    {
        if (role == CurrentRole)
        {
            return;
        }

        CurrentRole = role;
        Changed?.Invoke();
    }
}
