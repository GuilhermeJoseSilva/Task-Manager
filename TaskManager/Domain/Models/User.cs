using System.ComponentModel.DataAnnotations;
using TaskManager.Domain.Models;

namespace TaskManager.Domain.Models;

// Annotations to create the columns.
public class User
{
    [Key]
    public int Id { get; set; }
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [Required]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    //Navigation Property User Contains 1:N
    public ICollection<Task> Tasks { get; set; } = new List<Task>();
}