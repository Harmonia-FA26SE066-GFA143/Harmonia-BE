using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Harmonia.Infrastructure.ExternalServices;

public class PasswordHasherService : IPasswordHasherService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string HashPassword(User user, string password) => _hasher.HashPassword(user, password);

    public bool VerifyPassword(User user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}
