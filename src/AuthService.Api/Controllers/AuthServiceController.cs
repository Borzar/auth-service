using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using AuthService.Api.Dto;
using AuthService.Api.Model;
using AuthService.AuthDbContext;
using AuthService.Api.IJwtService;

[ApiController]
[Route("api/v1")]
public class AuthServiceController : ControllerBase
{
    private readonly IJwtService _jwtService;
    private readonly AuthDbContext _context;

    public AuthServiceController(IJwtService jwtService, AuthDbContext context)
    {
        _jwtService = jwtService;
        _context = context;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequestDto request)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        var exists = await _context.Users
            .AnyAsync(x =>
                x.Username == request.Username ||
                x.Email == request.Email);

        if (exists)
        {
            return BadRequest("El usuario ya existe");
        }

        _context.Users.Add(user);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            user.Id,
            user.Username,
            user.Email
        });
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequestDto request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x => 
                x.Username == request.UsernameOrEmail ||
                x.Email == request.UsernameOrEmail
            );

        if (user is null)
        {
            return Unauthorized();
        }

        var isValid = BCrypt.Net.BCrypt.Verify(
            request.Password,
            user.PasswordHash);

        if (!isValid)
        {
            return Unauthorized();
        }

        var accessToken = _jwtService.GenerateToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken(); 

        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _context.RefreshTokens.Add(refreshTokenEntity);

        await _context.SaveChangesAsync();


        return Ok(new
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenRequestDto request)
    {
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(x =>
                x.Token == request.RefreshToken);

        if (refreshToken == null)
            return Unauthorized("Refresh token inválido");

        if (refreshToken.ExpiresAt < DateTime.UtcNow)
            return Unauthorized("Refresh token expirado");

        var user = await _context.Users
            .FirstOrDefaultAsync(x =>
                x.Id == refreshToken.UserId);

        if (user == null)
            return Unauthorized();

        // Rotation start = Cada vez que usas un Refresh Token, se invalida el anterior y se genera uno nuevo.
        // Porque si alguien roba el refresh token anterior podrá usarlo durante los próximos 30 días.
        // Con Rotation La API:
            // 1. Valida el refresh token actual
            // 2. Genera nuevo Access Token
            // 3. Genera nuevo Refresh Token
            // 4. Elimina el refresh token anterior
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        var newRefreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = newRefreshToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _context.RefreshTokens.Remove(refreshToken);
        _context.RefreshTokens.Add(newRefreshTokenEntity);
        // Rotation end

        var accessToken = _jwtService.GenerateToken(user);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequestDto request)
    {
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(x =>
                x.Token == request.RefreshToken);

        if (refreshToken == null)
        {
            return Ok();
        }

        _context.RefreshTokens.Remove(refreshToken);

        await _context.SaveChangesAsync();

        return Ok();
    }

    [Authorize]
    [HttpGet("test-authorize")]
    public IActionResult Me()
    {
        return Ok(new
        {
            Username = User.FindFirst("username")?.Value,
            Email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
            Role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
        });
    }


    [Authorize(Roles = "Admin")]
    [HttpGet("test-admin")]
    public IActionResult AdminOnly()
    {
        return Ok("Solo Admin puede ver esto");
    }

}