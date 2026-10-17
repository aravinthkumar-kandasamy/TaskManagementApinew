
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using TaskManagementApi.Models;

namespace TaskManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private static readonly ConcurrentDictionary<Guid, TaskItem> Tasks = new();

    [HttpGet]
    public ActionResult<IEnumerable<TaskItem>> GetAll()
    {
        return Ok(Tasks.Values.OrderBy(t => t.CreatedAtUtc));
    }

    [HttpGet("{id:guid}")]
    public ActionResult<TaskItem> GetById(Guid id)
    {
        if (!Tasks.TryGetValue(id, out var task))
            return NotFound();

        return Ok(task);
    }

    [HttpPost]
    public ActionResult<TaskItem> Create(TaskItem task)
    {
        task.Id = Guid.NewGuid();
        task.CreatedAtUtc = DateTime.UtcNow;

        if (!Tasks.TryAdd(task.Id, task))
            return Conflict();

        return CreatedAtAction(
            nameof(GetById),
            new { id = task.Id },
            task);
    }

    [HttpPut("{id:guid}")]
    public ActionResult<TaskItem> Update(Guid id, TaskItem request)
    {
        if (!Tasks.TryGetValue(id, out var existing))
            return NotFound();

        var updated = new TaskItem
        {
            Id = id,
            Title = request.Title,
            Description = request.Description,
            IsCompleted = request.IsCompleted,
            CreatedAtUtc = existing.CreatedAtUtc
        };

        Tasks[id] = updated;

        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        if (!Tasks.TryRemove(id, out _))
            return NotFound();

        return NoContent();
    }
}
