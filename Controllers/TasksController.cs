
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using TaskManagementApi.Models;

namespace TaskManagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private static readonly ConcurrentDictionary<Guid, TaskItem> _tasks = new();

    // GET: api/tasks?page=1&pageSize=10
    // GET: api/tasks?isCompleted=true
    // GET: api/tasks?search=assessment
    // GET: api/tasks?sortBy=title&sortOrder=asc
    [HttpGet]
    public ActionResult<PagedResponse<TaskItem>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool? isCompleted = null,
        [FromQuery] string? search = null,
        [FromQuery] string sortBy = "createdAtUtc",
        [FromQuery] string sortOrder = "asc")
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

        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return BadRequest(new
            {
                message = "Sort field is required."
            });
        }

        if (!string.Equals(sortBy, "title",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sortBy, "createdAtUtc",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Supported sort fields are title and createdAtUtc."
            });
        }

        if (!string.Equals(sortOrder, "asc",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sortOrder, "desc",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Sort order must be asc or desc."
            });
        }

        // 1. Start with all tasks.
        IEnumerable<TaskItem> query = _tasks.Values;

        // 2. Filter by completion status.
        if (isCompleted.HasValue)
        {
            query = query.Where(task =>
                task.IsCompleted == isCompleted.Value);
        }

        // 3. Search task titles, ignoring letter case.
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(task =>
                task.Title.Contains(
                    search.Trim(),
                    StringComparison.OrdinalIgnoreCase));
        }

        // 4. Sort before pagination.
        var descending = string.Equals(
            sortOrder,
            "desc",
            StringComparison.OrdinalIgnoreCase);

        if (string.Equals(sortBy, "title",
                StringComparison.OrdinalIgnoreCase))
        {
            query = descending
                ? query.OrderByDescending(task => task.Title,
                    StringComparer.OrdinalIgnoreCase)
                    .ThenBy(task => task.Id)
                : query.OrderBy(task => task.Title,
                    StringComparer.OrdinalIgnoreCase)
                    .ThenBy(task => task.Id);
        }
        else
        {
            query = descending
                ? query.OrderByDescending(task => task.CreatedAtUtc)
                    .ThenBy(task => task.Id)
                : query.OrderBy(task => task.CreatedAtUtc)
                    .ThenBy(task => task.Id);
        }

        // 5. Count filtered records before pagination.
        var filteredTasks = query.ToList();
        var totalCount = filteredTasks.Count;

        // Avoid integer overflow for very large page numbers.
        var offset = (int)Math.Min(
            ((long)page - 1) * pageSize,
            int.MaxValue);

        var items = filteredTasks
            .Skip(offset)
            .Take(pageSize)
            .ToList();

        return Ok(new PagedResponse<TaskItem>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(
                totalCount / (double)pageSize)
        });
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
