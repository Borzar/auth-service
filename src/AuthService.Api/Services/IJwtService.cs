using AuthService.Api.Model;

namespace AuthService.Api.Services;

public interface IJwtService
{
    string GenerateToken(User user);
    string GenerateRefreshToken();

}