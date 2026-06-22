using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using AuthService.Api.Dto;
using AuthService.Api.Model;
using AuthService.Api.Services;
using Moq;
using Xunit;

// LE DAMOS UN ALIAS: Aquí le decimos a C# que cuando escribamos "RealDbContext", 
// se refiere específicamente a la CLASE interna dentro de ese namespace conflictivo.
using RealDbContext = AuthService.AuthDbContext.AuthDbContext;

namespace AuthService.Tests
{
    public class AuthServiceControllerTest : IDisposable
    {
        private readonly Mock<IJwtService> _jwtServiceMock;
        private readonly RealDbContext _context;
        private readonly SqliteConnection _connection;
        private readonly AuthServiceController _controller;

        /*
            Why we should use SQLite in our test?

            Con Postgres: Para que el test corra, necesitas tener un servidor de PostgreSQL instalado en tu máquina (o un contenedor Docker), 
            levantar el servicio, crear una base de datos en el disco duro, conectar el test, vaciar las tablas al terminar, etc. 
            Esto hace que las pruebas sean lentas.

            Con SQLite In-Memory: Al usar "DataSource=:memory:", la base de datos entera se crea en la memoria RAM en microsegundos y se 
            destruye instantáneamente cuando el test termina. No deja basura en tu computadora y los tests corren a la velocidad de la luz.

            Integración Continua (GitHub Actions / GitLab CI)
            Cuando subas tu código a GitHub y configures un pipeline para que corra tus tests automáticamente en la nube antes de hacer un despliegue, 
            esos servidores virtuales no vienen con PostgreSQL instalado.
            Si usas Postgres, tendrías que configurar scripts complejos en tu pipeline para levantar un contenedor de Postgres en la nube solo para los tests.
            Si usas SQLite, el test corre directamente en cualquier máquina porque la base de datos vive dentro de la memoria del propio proceso de .NET. 
            No necesita instalar nada externo.
        */

        /*
            What are we testing? 
            Si cambiamos el motor de base de datos a SQLite, parece que estuviéramos haciendo trampa, ¿verdad? 
            Pero la realidad es que no estás testeando el motor de la base de datos; estás testeando el comportamiento de tu controlador y tus reglas de negocio.

            1. Estamos validando que toda la "fontanería" y la lógica en C# que tú escribiste funcione exactamente como esperas.
                Por ejemplo: Metodo Register, 
                - Validar que el if (exists) realmente intercepte la solicitud si el usuario ya está guardado.
                - Verificar que devuelva un BadRequest con el mensaje "El usuario ya existe". Si tu lógica de filtros estuviera mal redactada en C#, el test saltaría en rojo.

            2. Encriptación de contraseñas funcione en un flujo real de principio a fin:
                En Register: 
                - Validas que BCrypt.HashPassword devuelva una cadena segura que se almacene en la propiedad PasswordHash.
                - En Login: Validas que BCrypt.Verify sea capaz de tomar la contraseña en texto plano que envía el usuario, 
                - compararla contra el hash que recuperó Entity Framework de la base de datos (SQLite) y darla por válida.

            3. Los Tipos de Resultados HTTP (ActionResults):
               Estás asegurando que tu API responda con los códigos de estado HTTP correctos según los estándares REST:
                - Si todo sale bien, que devuelva un OkObjectResult (HTTP 200).
                - Si las credenciales fallan, que devuelva un UnauthorizedObjectResult (HTTP 401).
                - Si hay duplicados, un BadRequestObjectResult (HTTP 400).
        */


        public AuthServiceControllerTest()
        {
            _jwtServiceMock = new Mock<IJwtService>();

            // 1. Setup SQLite In-Memory connection to substitute Npgsql during testing
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<RealDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new RealDbContext(options);
            _context.Database.EnsureCreated();

            // 2. Instantiate the controller with mocked and in-memory dependencies
            _controller = new AuthServiceController(_jwtServiceMock.Object, _context);
        }

        [Fact]
        public async Task Register_ShouldCreateUser_WhenRequestIsValid()
        {
            // Arrange
            var request = new RegisterRequestDto 
            { 
                Username = "testuser", 
                Email = "test@test.com", 
                Password = "SecretPassword123" 
            };

            // Act
            var result = await _controller.Register(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            // Verify real persistence in the SQLite Database
            var userInDb = await _context.Users.FirstOrDefaultAsync(u => u.Username == "testuser");
            Assert.NotNull(userInDb);
            Assert.Equal("test@test.com", userInDb.Email);
            
            // Verify password was hashed securely with BCrypt
            Assert.NotEqual("SecretPassword123", userInDb.PasswordHash);
        }

        [Fact]
        public async Task Register_ShouldReturnBadRequest_WhenUsernameOrEmailAlreadyExists()
        {
            // Arrange: Seed an existing user directly into the database
            _context.Users.Add(new User 
            { 
                Id = Guid.NewGuid(), 
                Username = "existinguser", 
                Email = "existing@test.com", 
                PasswordHash = "fakehash" 
            });
            await _context.SaveChangesAsync();

            var duplicateRequest = new RegisterRequestDto 
            { 
                Username = "existinguser", 
                Email = "other@test.com", 
                Password = "Password123" 
            };

            // Act
            var result = await _controller.Register(duplicateRequest);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            
            // Ensure no duplicate records were added
            var totalUsers = await _context.Users.CountAsync();
            Assert.Equal(1, totalUsers);
        }

        [Fact]
        public async Task Login_ShouldReturnTokens_WhenCredentialsAreCorrect()
        {
            // Arrange: Create a user with a hashed password
            var rawPassword = "MySecurePassword";
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(rawPassword);
            var user = new User 
            { 
                Id = Guid.NewGuid(), 
                Username = "boris", 
                Email = "boris@test.com", 
                PasswordHash = passwordHash 
            };
            
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Setup JwtService Mock behavior
            _jwtServiceMock.Setup(x => x.GenerateToken(It.IsAny<User>())).Returns("fake-access-token");
            _jwtServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("fake-refresh-token");

            var request = new LoginRequestDto { UsernameOrEmail = "boris", Password = rawPassword };

            // Act
            var result = await _controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            // Verify the Refresh Token entity was correctly stored in the Database
            var storedToken = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.UserId == user.Id);
            Assert.NotNull(storedToken);
            Assert.Equal("fake-refresh-token", storedToken.Token);
        }

        [Fact]
        public async Task Refresh_ShouldRotateTokensAndRemoveOldOne_WhenTokenIsValid()
        {
            // Arrange: Seed a user and an active refresh token
            var userId = Guid.NewGuid();
            var oldTokenValue = "old-refresh-token-123";
            
            _context.Users.Add(new User { Id = userId, Username = "user", Email = "u@u.com", PasswordHash = "h" });
            _context.RefreshTokens.Add(new RefreshToken 
            { 
                Id = Guid.NewGuid(), 
                UserId = userId, 
                Token = oldTokenValue, 
                ExpiresAt = DateTime.UtcNow.AddDays(1) // Not expired
            });
            await _context.SaveChangesAsync();

            // Setup mocks for the token rotation generation
            _jwtServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("new-refresh-token-456");
            _jwtServiceMock.Setup(x => x.GenerateToken(It.IsAny<User>())).Returns("new-access-token");

            var request = new RefreshTokenRequestDto { RefreshToken = oldTokenValue };

            // Act
            var result = await _controller.Refresh(request);

            // Assert
            Assert.IsType<OkObjectResult>(result);

            // 1. Verify the old token was successfully rotated out (removed)
            var oldTokenExists = await _context.RefreshTokens.AnyAsync(t => t.Token == oldTokenValue);
            Assert.False(oldTokenExists);

            // 2. Verify the new token is securely saved in the database
            var newTokenExists = await _context.RefreshTokens.AnyAsync(t => t.Token == "new-refresh-token-456");
            Assert.True(newTokenExists);
        }

        [Fact]
        public async Task Logout_ShouldRemoveTokenFromDatabase_WhenTokenExists()
        {
            // Arrange
            var tokenToRevoke = "logout-token-xyz";
            _context.RefreshTokens.Add(new RefreshToken 
            { 
                Id = Guid.NewGuid(), 
                UserId = Guid.NewGuid(), 
                Token = tokenToRevoke, 
                ExpiresAt = DateTime.UtcNow.AddDays(5) 
            });
            await _context.SaveChangesAsync();

            var request = new LogoutRequestDto { RefreshToken = tokenToRevoke };

            // Act
            var result = await _controller.Logout(request);

            // Assert
            Assert.IsType<OkResult>(result);
            
            // Verify database deletion
            var tokenExists = await _context.RefreshTokens.AnyAsync(t => t.Token == tokenToRevoke);
            Assert.False(tokenExists);
        }

        // Cleanup after each execution to clear RAM memory structures
        public void Dispose()
        {
            _context?.Dispose();
            _connection?.Dispose();
        }
    }
}