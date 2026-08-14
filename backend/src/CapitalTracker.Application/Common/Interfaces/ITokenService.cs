using CapitalTracker.Domain.Entities;

namespace CapitalTracker.Application.Common.Interfaces;

public interface ITokenService
{
    string GenerateToken(User user);
}
