using System.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using AuthService.Api.IJwtService;
using AuthService.Api.Model;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
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
                _configuration["Jwt:Key"]!));

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
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
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