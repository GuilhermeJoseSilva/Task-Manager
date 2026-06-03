using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using TaskStatus = TaskManager.Domain.Enums.TaskStatus;

namespace TaskManager.DTOs;

public record LoginDto(
    [Required] string Email,
    [Required] string Password
);

public record RegisterDto(
    [Required][MaxLength(100)] string Name,
    [Required][EmailAddress] string Email,
    [Required][MinLength(6)] string Password
);

public record TokenResponseDto(
    string Token,
    DateTime ExpiresAt
);

public record UserResponseDto(
    int Id,
    string Name,
    string Email,
    DateTime CreatedAt
);

public record TaskCreateDto(
    [Required][MaxLength(200)] string Title,
    string? Description,
    [Required] DateTime DueDate,
    [Required] int UserId
);

public record UpdateStatusDto(
    [Required] TaskStatus NewStatus
);

public record TaskResponseDto(
    int Id,
    string Title,
    string? Description,
    TaskStatus Status,
    DateTime DueDate,
    DateTime CreatedAt,
    string UserName
);

// Dapper report
public record TaskSummaryDto(
    string UserName,
    string UserEmail,
    int TotalTasks,
    int Pending,
    int InProgress,
    int Completed,
    int Overdue
);