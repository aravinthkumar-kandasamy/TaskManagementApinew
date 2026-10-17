
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using TaskManagementApi.Models;

namespace TaskManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private static readonly ConcurrentDictionary<Guid, TaskItem> _tasks = new();

    // GET: api/tasks?page=1&pageSize=10
    [HttpGet]
    public ActionResult<PagedResponse<TaskItem>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1)
        {
            return BadRequest(new
            {
                message = "Page must be greater than or equal to 1."
            });
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new
            {
                message = "Page size must be between 1 and 100."
            });
        }

        var allTasks = _tasks.Values
            .OrderBy(task => task.CreatedAtUtc)
            .ThenBy(task => task.Id)
            .ToList();

        var totalCount = allTasks.Count;

        var items = allTasks
            .Skip((int)Math.Min(((long)page - 1) * pageSize, int.MaxValue))
            .Take(pageSize)
            .ToList();

        var response = new PagedResponse<TaskItem>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(
                totalCount / (double)pageSize)
        };

        return Ok(response);
    }

    // GET: api/tasks/{id}
    [HttpGet("{id:guid}")]
    public ActionResult<TaskItem> GetById(Guid id)
    {
        if (!_tasks.TryGetValue(id, out var task))
        {
            return NotFound(new
            {
                message = $"Task with ID '{id}' was not found."
            });
        }

        return Ok(task);
    }

    // POST: api/tasks
    [HttpPost]
    public ActionResult<TaskItem> Create([FromBody] TaskItem task)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        task.Id = Guid.NewGuid();
        task.CreatedAtUtc = DateTime.UtcNow;

        if (!_tasks.TryAdd(task.Id, task))
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = "Unable to create the task." });
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = task.Id },
            task);
    }

    // PUT: api/tasks/{id}
    [HttpPut("{id:guid}")]
    public ActionResult<TaskItem> Update(
        Guid id,
        [FromBody] TaskItem updatedTask)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!_tasks.TryGetValue(id, out var existingTask))
        {
            return NotFound(new
            {
                message = $"Task with ID '{id}' was not found."
            });
        }

        var taskToUpdate = new TaskItem
        {
            Id = existingTask.Id,
            Title = updatedTask.Title,
            Description = updatedTask.Description,
            IsCompleted = updatedTask.IsCompleted,
            CreatedAtUtc = existingTask.CreatedAtUtc
        };

        if (!_tasks.TryUpdate(id, taskToUpdate, existingTask))
        {
            return Conflict(new
            {
                message = "The task was modified by another request. Please retry."
            });
        }

        return Ok(taskToUpdate);
    }

    // DELETE: api/tasks/{id}
    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        if (!_tasks.TryRemove(id, out _))
        {
            return NotFound(new
            {
                message = $"Task with ID '{id}' was not found."
            });
        }

        return NoContent();
    }
}
