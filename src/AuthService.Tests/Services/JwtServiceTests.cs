using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using AuthService.Api.Model;
using AuthService.Api.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace MiProyecto.Tests
{
    public class JwtServiceTests
    {
        private readonly Mock<IConfiguration> _configMock;
        private readonly JwtSettings _jwtSettings;
        private readonly JwtService _jwtService;

        public JwtServiceTests()
        {
            _configMock = new Mock<IConfiguration>();
            
            // Creamos una configuración de prueba dummy con una clave segura (min 256 bits / 32 caracteres)
            _jwtSettings = new JwtSettings
            {
                Key = "EstaEsUnaClaveSuperSecretaYMuyLargaDePrueba123!",
                Issuer = "AuthService",
                Audience = "TrackerSuite",
                Expiration = 60
            };

            _jwtService = new JwtService(_configMock.Object, _jwtSettings);
        }

        [Fact]
        public void GenerateToken_ShoudReturnJwtValid_WithValidClaims()
        {
            // Arrange
            var idTest = Guid.NewGuid();
            var usuarioPrueba = new User
            {
                Id = idTest,
                Username = "jhon",
                Email = "jhon@test.com",
                Role = "Admin"
            };

            // Act
            var tokenString = _jwtService.GenerateToken(usuarioPrueba);

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(tokenString));

            // Para leer el token generado y validar su interior:
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(tokenString);

            // 1. Validar Header/Estructura básica
            Assert.Equal("HS256", jwtToken.Header.Alg);
            Assert.Equal(_jwtSettings.Issuer, jwtToken.Issuer);
            Assert.Contains(_jwtSettings.Audience, jwtToken.Audiences);

            // 2. Validar Claims del Payload
            var claimSub = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            var claimUsername = jwtToken.Claims.FirstOrDefault(c => c.Type == "username")?.Value;
            var claimEmail = jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
            var claimRole = jwtToken.Claims.FirstOrDefault(c => c.Type == "role")?.Value;

            Assert.Equal(idTest.ToString(), claimSub);
            Assert.Equal("jhon", claimUsername);
            Assert.Equal("jhon@test.com", claimEmail);
            Assert.Equal("Admin", claimRole);

            // 3. Validar Tiempo de Expiración (Margen de error de unos segundos por la ejecución)
            var diferenciaTiempo = jwtToken.ValidTo - DateTime.UtcNow;
            Assert.True(diferenciaTiempo.TotalMinutes > 59 && diferenciaTiempo.TotalMinutes <= 60);
        }

        [Fact]
        public void GenerateRefreshToken_ShouldReturnValidAndUniqueBase64String()
        {
            // Act
            var token1 = _jwtService.GenerateRefreshToken();
            var token2 = _jwtService.GenerateRefreshToken();

            // Assert
            Assert.NotNull(token1);
            Assert.NotEmpty(token1);
            
            // Debe ser único (la probabilidad de que RandomNumberGenerator repita 64 bytes es nula)
            Assert.NotEqual(token1, token2);

            // Verificar que sea un Base64 válido intentando convertirlo de vuelta
            var bytes = Convert.FromBase64String(token1);
            Assert.True(bytes.Length > 0);
        }
    }
}