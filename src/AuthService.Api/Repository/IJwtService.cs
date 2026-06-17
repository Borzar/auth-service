using AuthService.Api.Model;

namespace AuthService.Api.IJwtService;

public interface IJwtService
{
    string GenerateToken(User user);
    string GenerateRefreshToken();

}