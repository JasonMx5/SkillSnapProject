using SkillSnap.Shared.Models;

namespace SkillSnap.Client.Services;

public class UserSessionService
{
    private string? _userId;
    private string? _role;
    private Project? _currentProject;

    public string? UserId => _userId;

    public string? Role => _role;

    public Project? CurrentProject
    {
        get => _currentProject;
        set
        {
            if (ReferenceEquals(_currentProject, value))
            {
                return;
            }

            _currentProject = value;
            StateChanged?.Invoke();
        }
    }

    public event Action? StateChanged;

    public void SetIdentity(string? userId, string? role)
    {
        if (_userId == userId && _role == role)
        {
            return;
        }

        _userId = userId;
        _role = role;
        StateChanged?.Invoke();
    }
}
