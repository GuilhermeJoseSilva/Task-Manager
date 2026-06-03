using TaskManager.Data.Repositories;
using TaskManager.Domain.Enums;
using TaskManager.Domain.Models;
using TaskManager.DTOs;

namespace TaskManager.Services;

public interface ITaskService
{
    Task<TaskResponseDto> CreateAsync(TaskCreateDto dto);
    Task<TaskResponseDto?> GetByIdAsync(int id);
    Task<List<TaskResponseDto>> ListByUserAsync(int userId);
    System.Threading.Tasks.Task UpdateStatusAsync(int id, TaskManager.Domain.Enums.TaskStatus newStatus, string changedBy);
    Task<List<TaskSummaryDto>> GetSummaryByUserAsync(int userId);
}

public class TaskService : ITaskService
{
     private readonly ITaskRepository _taskRepo;
    private readonly IUserRepository _userRepo;
    private readonly ILogger<TaskService> _logger;

    public TaskService(
        ITaskRepository taskRepo,
        IUserRepository userRepo,
        ILogger<TaskService> logger)
    {
        _taskRepo = taskRepo;
        _userRepo = userRepo;
        _logger = logger;
    }

    public async Task<TaskResponseDto> CreateAsync(TaskCreateDto dto)
    {
        var user = await _userRepo.GetByIdAsync(dto.UserId)
            ?? throw new KeyNotFoundException($"User {dto.UserId} not found.");

        var task = new TaskManager.Domain.Models.Task
        {
            Title = dto.Title,
            Description = dto.Description,
            DueDate = dto.DueDate.ToUniversalTime(),
            UserId = dto.UserId,
            Status = TaskManager.Domain.Enums.TaskStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _taskRepo.AddAsync(task);

        // Logger — registra o evento no console
        // Em produção isso vai pro OpenTelemetry/Grafana
        _logger.LogInformation(
            "Task {Id} created for user {UserId}",
            created.Id, dto.UserId);

        return MapToDto(created, user.Name);
    }

    public async Task<TaskResponseDto?> GetByIdAsync(int id)
    {
        var task = await _taskRepo.GetByIdAsync(id);
        if (task == null) return null;

        return MapToDto(task, task.User.Name);
    }

    public async Task<List<TaskResponseDto>> ListByUserAsync(int userId)
    {
        var tasks = await _taskRepo.GetByUserIdAsync(userId);
        return tasks.Select(t => MapToDto(t, t.User.Name)).ToList();
    }

    public async System.Threading.Tasks.Task UpdateStatusAsync(int id, TaskManager.Domain.Enums.TaskStatus newStatus, string changedBy)
    {
        var task = await _taskRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Task {id} not found.");

        // Regra de negócio — tarefa arquivada não pode mudar de status
        if (task.Status == TaskManager.Domain.Enums.TaskStatus.Archived)
            throw new InvalidOperationException("Archived task cannot be changed.");

        var previousStatus = task.Status;

        // Cria o registro de histórico — audit trail
        // Toda mudança de status fica registrada com quem fez e quando
        var history = new StatusHistory
        {
            TaskId = id,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ChangedAt = DateTime.UtcNow,
            ChangedBy = changedBy
        };

        var updatedTask = new TaskManager.Domain.Models.Task
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            Status = newStatus,
            DueDate = task.DueDate,
            UserId = task.UserId,
            CreatedAt = task.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };

        await _taskRepo.UpdateAsync(updatedTask);

        _logger.LogInformation(
            "Task {Id} status changed from {Previous} to {New} by {Who}",
            id, previousStatus, newStatus, changedBy);
    }

    public async Task<List<TaskSummaryDto>> GetSummaryByUserAsync(int userId)
    {
        return await _taskRepo.GetSummaryByUserAsync(userId);
    }

    private static TaskResponseDto MapToDto(
        TaskManager.Domain.Models.Task task, string userName) =>
        new(
            task.Id,
            task.Title,
            task.Description,
            task.Status,
            task.DueDate,
            task.CreatedAt,
            userName
        );
}