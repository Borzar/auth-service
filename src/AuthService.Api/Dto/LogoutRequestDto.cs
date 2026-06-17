namespace AuthService.Api.Dto;

public class LogoutRequestDto
{
    public string RefreshToken { get; set; } = string.Empty;
}