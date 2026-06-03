using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TaskStatus = TaskManager.Domain.Enums.TaskStatus;
using Task = TaskManager.Domain.Models.Task;

namespace TaskManager.Domain.Models;

public class StatusHistory
{
    [Key]
    public int Id { get; set; }
    public TaskStatus PreviousStatus { get; set; }
    public TaskStatus NewStatus { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    // Null -> Job || Name -> User
    public string? ChangedBy { get; set; }

    [ForeignKey("Task")]
    public int TaskId { get; set; }
    public Task Task { get; set; } = null!;
}