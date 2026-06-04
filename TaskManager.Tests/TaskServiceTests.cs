using Moq;
using Microsoft.Extensions.Logging;
using TaskManager.Data.Repositories;
using TaskManager.Domain.Enums;
using TaskManager.Domain.Models;
using TaskManager.DTOs;
using TaskManager.Services;

namespace TaskManager.Tests;

public class TaskServiceTests
{
    private readonly Mock<ITaskRepository> _taskRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<ILogger<TaskService>> _loggerMock;
    private readonly TaskService _service;

    public TaskServiceTests()
    {
        _taskRepoMock = new Mock<ITaskRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _loggerMock = new Mock<ILogger<TaskService>>();

        _service = new TaskService(_taskRepoMock.Object, _userRepoMock.Object, _loggerMock.Object);
    }

    public async System.Threading.Tasks.Task CreateAsync_UserExists_ReturnsCreatedTask()
    {
        var fakeUser = new User
        {
            Id = 1,
            Name = "Guilherme",
            Email = "gui@email.com",
            PasswordHash = "hash"
        };

        var dto = new TaskCreateDto(
            "Estudar EF Core",
            "Diferenças entre EF Core vs Dapper",
            DateTime.UtcNow.AddDays(3),
            UserId: 1
        );

        _userRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(fakeUser);

        _taskRepoMock.Setup(r => r.AddAsync(It.IsAny<TaskManager.Domain.Models.Task>()))
            .ReturnsAsync((TaskManager.Domain.Models.Task t) =>
            {
                t.Id = 1;
                return t;
            }
        );

        var result = await _service.CreateAsync(dto);

        Assert.NotNull(result);
        Assert.Equal("Estudar EF Core", result.Title);
        Assert.Equal(Domain.Enums.TaskStatus.Pending, result.Status);
        Assert.Equal("Guilherme", result.UserName);

        _taskRepoMock.Verify(r => r.AddAsync(
            It.IsAny<TaskManager.Domain.Models.Task>()
        ), Times.Once);
    }

    [Fact]
    public async System.Threading.Tasks.Task CreateAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        var dto = new TaskCreateDto(
            "Tarefa", null, DateTime.UtcNow.AddDays(1), UserId: 99
        );

        _userRepoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.CreateAsync(dto)
        );

        _taskRepoMock.Verify(r => r.AddAsync(
            It.IsAny<TaskManager.Domain.Models.Task>()), Times.Never
        );
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateStatusAsync_ArchivedTask_ThrowsInvalidOperationException()
    {
        var archivedTask = new TaskManager.Domain.Models.Task
        {
            Id = 1,
            Title = "Task antiga",
            Status = TaskManager.Domain.Enums.TaskStatus.Archived,
            UserId = 1,
            DueDate = DateTime.UtcNow.AddDays(-30),
            User = new User {Id = 1, Name = "Gui", Email = "g@g.com", PasswordHash = "h"}
        };

        _taskRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(archivedTask);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateStatusAsync(1, TaskManager.Domain.Enums.TaskStatus.Pending, "gui@email.com")
        );
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateStatusAsync_ValidTask_CallsUpdateOnRepository()
    {
        var task = new TaskManager.Domain.Models.Task
        {
         Id = 1,
            Title = "Minha task",
            Status = TaskManager.Domain.Enums.TaskStatus.Pending,
            UserId = 1,
            DueDate = DateTime.UtcNow.AddDays(5),
            User = new User { Id = 1, Name = "Gui", Email = "g@g.com", PasswordHash = "h" }
        };

        _taskRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(task);

        _taskRepoMock.Setup(r => r.UpdateAsync(It.IsAny<TaskManager.Domain.Models.Task>()))
            .Returns(System.Threading.Tasks.Task.CompletedTask);

        await _service.UpdateStatusAsync(1, TaskManager.Domain.Enums.TaskStatus.InProgress, "gui@email.com");

        _taskRepoMock.Verify(
            r => r.UpdateAsync(It.Is<TaskManager.Domain.Models.Task>(
                t => t.Status == TaskManager.Domain.Enums.TaskStatus.InProgress)),
            Times.Once
        );
    }

    // Theory = mesmo teste com dados diferentes
    // Evita copiar e colar o mesmo teste várias vezes
    [Theory]
    [InlineData(TaskManager.Domain.Enums.TaskStatus.Pending, TaskManager.Domain.Enums.TaskStatus.InProgress)]
    [InlineData(TaskManager.Domain.Enums.TaskStatus.InProgress, TaskManager.Domain.Enums.TaskStatus.Completed)]
    [InlineData(TaskManager.Domain.Enums.TaskStatus.Pending, TaskManager.Domain.Enums.TaskStatus.Overdue)]
    public async System.Threading.Tasks.Task UpdateStatusAsync_ValidTransitions_DoesNotThrow(
        TaskManager.Domain.Enums.TaskStatus initialStatus,
        TaskManager.Domain.Enums.TaskStatus newStatus)
    {
        var task = new TaskManager.Domain.Models.Task
        {
            Id = 1,
            Title = "Teste",
            Status = initialStatus,
            UserId = 1,
            DueDate = DateTime.UtcNow.AddDays(1),
            User = new User { Id = 1, Name = "Gui", Email = "g@g.com", PasswordHash = "h" }
        };

        _taskRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(task);
        _taskRepoMock.Setup(r => r.UpdateAsync(
            It.IsAny<TaskManager.Domain.Models.Task>()))
            .Returns(System.Threading.Tasks.Task.CompletedTask);

        // Não deve lançar nenhuma exception
        var exception = await Record.ExceptionAsync(
            () => _service.UpdateStatusAsync(1, newStatus, "gui")
        );

        Assert.Null(exception);
    }
}