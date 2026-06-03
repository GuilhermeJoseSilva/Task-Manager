using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TaskStatus = TaskManager.Domain.Enums.TaskStatus;

namespace TaskManager.Domain.Models;

public class Task
{
    [Key]
    public int Id { get; set; }
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    // EF maps the enum Into the DB CONTEXT
    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    public DateTime DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    [ForeignKey("User")]
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public ICollection<StatusHistory> StatusHistory { get; set; } = new List<StatusHistory>();
}