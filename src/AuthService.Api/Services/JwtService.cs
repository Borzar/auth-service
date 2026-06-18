using System.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using AuthService.Api.Model;
using AuthService.Api.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    private readonly JwtSettings _jwtSettings;

    public JwtService(IConfiguration configuration, JwtSettings jwtSettings)
    {
        _configuration = configuration;
        _jwtSettings = jwtSettings;
    }

    public string GenerateToken(User user)
    {

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("username", user.Username),
            new Claim("email", user.Email),
            new Claim("role", user.Role)
        };

        // Crear clave secreta
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                _jwtSettings.Key));

        // Crear firma
        var credentials =
            new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

        // Crear JWT
        // Issuer: InvalidCastException quien emitio el token 
        // Audiencie: Indica quien puede consumirlo
        // Firma el token con HMAC SHA256.

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.Expiration),
            signingCredentials: credentials);

        // Convierte el objeto JWT a texto.
        // Contenido del token
        /* 
             Header 
            {
                "alg": "HS256",
                "typ": "JWT"
            }
            Payload
            {
                "name": "boris",
                "iss": "AuthService",
                "aud": "TrackerSuite",
                "exp": 1780000000
            }
            Signature: Firma criptográfica

        */
        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);

        return Convert.ToBase64String(randomBytes);
    }
}