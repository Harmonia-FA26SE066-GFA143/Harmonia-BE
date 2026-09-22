using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IServices;

public interface IPasswordHasherService
{
    string HashPassword(User user, string password);

    bool VerifyPassword(User user, string password);
}
