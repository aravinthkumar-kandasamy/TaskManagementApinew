
using System.ComponentModel.DataAnnotations;

namespace TaskManagementApi.Models;

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(150, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
