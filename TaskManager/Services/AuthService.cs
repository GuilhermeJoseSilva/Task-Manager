using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TaskManager.Data.Repositories;
using TaskManager.Domain.Models;
using TaskManager.DTOs;

namespace TaskManager.Services;

public interface IAuthService
{
    Task<TokenResponseDto> LoginAsync(LoginDto dto);
    Task<UserResponseDto> RegisterAsync(RegisterDto dto);
}

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IConfiguration _config;

    public AuthService(IUserRepository userRepo, IConfiguration config)
    {
        _userRepo = userRepo;
        _config = config;
    }

    public async Task<UserResponseDto> RegisterAsync(RegisterDto dto)
    {
        if (await _userRepo.ExistsAsync(dto.Email))
            throw new InvalidOperationException("Email already registered.");

        var user = new User
        {
            Name = dto.Name,
            Email = dto.Email,
            // BCrypt with automatic SALT
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            CreatedAt = DateTime.UtcNow
        };

        var created = await _userRepo.AddAsync(user);

        return new UserResponseDto(created.Id, created.Name, created.Email, created.CreatedAt);
    }

    public async Task<TokenResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _userRepo.GetByEmailAsync(dto.Email)
            ?? throw new UnauthorizedAccessException("Invalid credentials.");

        // BCrypt verifica o hash sem expor a senha original
        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        return GenerateToken(user);
    }

    private TokenResponseDto GenerateToken(User user)
    {
        var secretKey = _config["Jwt:SecretKey"]!;
        var expiresInHours = int.Parse(_config["Jwt:ExpiresInHours"]!);
        var expiresAt = DateTime.UtcNow.AddHours(expiresInHours);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Claims = dados que ficam dentro do token JWT
        // O middleware de autenticação valida e expõe isso automaticamente
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name)
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return new TokenResponseDto(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt
        );
    }
}