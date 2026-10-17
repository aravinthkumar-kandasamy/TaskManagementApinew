
using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TaskManagementApi.Models;
using Xunit;

namespace TaskManagementApi.Tests;

public class TasksApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public TasksApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/api/tasks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result =
            await response.Content.ReadFromJsonAsync<PagedResponse<TaskItem>>();

        Assert.NotNull(result);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task Create_WithValidTask_ReturnsCreated()
    {
        var task = new TaskItem
        {
            Title = "Complete assessment",
            Description = "Write automated tests",
            IsCompleted = false
        };

        var response = await _client.PostAsJsonAsync("/api/tasks", task);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created =
            await response.Content.ReadFromJsonAsync<TaskItem>();

        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(task.Title, created.Title);
    }

    [Fact]
    public async Task Create_WithShortTitle_ReturnsBadRequest()
    {
        var task = new TaskItem { Title = "AB" };

        var response = await _client.PostAsJsonAsync("/api/tasks", task);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithUnknownId_ReturnsNotFound()
    {
        var response =
            await _client.GetAsync($"/api/tasks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithUnknownId_ReturnsNotFound()
    {
        var response =
            await _client.DeleteAsync($"/api/tasks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithPageSizeTwo_ReturnsAtMostTwoItems()
    {
        for (var i = 1; i <= 3; i++)
        {
            var createResponse = await _client.PostAsJsonAsync(
                "/api/tasks",
                new TaskItem { Title = $"Pagination task {i}" });

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        }

        var response =
            await _client.GetAsync("/api/tasks?page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result =
            await response.Content.ReadFromJsonAsync<PagedResponse<TaskItem>>();

        Assert.NotNull(result);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(2, result.Items.Count);
        Assert.True(result.TotalCount >= 3);
    }

    [Fact]
    public async Task GetAll_SearchByTitle_ReturnsMatchingTasks()
    {
        const string uniqueTitle = "PR4-search-unique-task";

        var createResponse = await _client.PostAsJsonAsync(
            "/api/tasks",
            new TaskItem { Title = uniqueTitle });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var response = await _client.GetAsync(
            $"/api/tasks?search={uniqueTitle}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result =
            await response.Content.ReadFromJsonAsync<PagedResponse<TaskItem>>();

        Assert.NotNull(result);
        Assert.Contains(result.Items, item => item.Title == uniqueTitle);
    }

    [Fact]
    public async Task GetAll_FilterByCompletion_ReturnsMatchingTasks()
    {
        const string uniqueTitle = "PR4-completed-unique-task";

        var createResponse = await _client.PostAsJsonAsync(
            "/api/tasks",
            new TaskItem
            {
                Title = uniqueTitle,
                IsCompleted = true
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var response = await _client.GetAsync(
            $"/api/tasks?isCompleted=true&search={uniqueTitle}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result =
            await response.Content.ReadFromJsonAsync<PagedResponse<TaskItem>>();

        Assert.NotNull(result);
        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item =>
        {
            Assert.True(item.IsCompleted);
            Assert.Contains(
                uniqueTitle,
                item.Title,
                StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task GetAll_SortByTitleAscending_ReturnsSortedTasks()
    {
        const string prefix = "PR4-sort-unique-";

        foreach (var title in new[]
        {
            $"{prefix}Beta",
            $"{prefix}Alpha"
        })
        {
            var createResponse = await _client.PostAsJsonAsync(
                "/api/tasks",
                new TaskItem { Title = title });

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        }

        var response = await _client.GetAsync(
            $"/api/tasks?search={prefix}&sortBy=title&sortOrder=asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result =
            await response.Content.ReadFromJsonAsync<PagedResponse<TaskItem>>();

        Assert.NotNull(result);

        var titles = result.Items.Select(item => item.Title).ToList();

        Assert.Equal(
            new[] { $"{prefix}Alpha", $"{prefix}Beta" },
            titles);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 101)]
    public async Task GetAll_WithInvalidPagination_ReturnsBadRequest(
        int page,
        int pageSize)
    {
        var response = await _client.GetAsync(
            $"/api/tasks?page={page}&pageSize={pageSize}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("invalid", "asc")]
    [InlineData("title", "invalid")]
    [InlineData("id", "asc")]
    public async Task GetAll_WithInvalidSorting_ReturnsBadRequest(
        string sortBy,
        string sortOrder)
    {
        var response = await _client.GetAsync(
            $"/api/tasks?sortBy={sortBy}&sortOrder={sortOrder}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    [Fact]
    public async Task GetAll_FilterByCompletion_ReturnsOnlyMatchingTasks()
    {
        // Arrange: create two tasks with the same title
        // but different completion statuses.
        const string uniqueTitle = "PR4-completion-filter-test";

        var completedTask = new TaskItem
        {
            Title = uniqueTitle,
            IsCompleted = true
        };

        var incompleteTask = new TaskItem
        {
            Title = uniqueTitle,
            IsCompleted = false
        };

        var completedResponse = await _client.PostAsJsonAsync(
            "/api/tasks", completedTask);

        var incompleteResponse = await _client.PostAsJsonAsync(
            "/api/tasks", incompleteTask);

        Assert.Equal(
            HttpStatusCode.Created, completedResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.Created, incompleteResponse.StatusCode);

        // Act: request completed tasks.
        var completedFilterResponse = await _client.GetAsync(
            $"/api/tasks?isCompleted=true&search={uniqueTitle}");

        Assert.Equal(
            HttpStatusCode.OK, completedFilterResponse.StatusCode);

        var completedResult =
            await completedFilterResponse.Content
                .ReadFromJsonAsync<PagedResponse<TaskItem>>();

        // Assert: only the completed task is returned.
        Assert.NotNull(completedResult);
        Assert.Single(completedResult.Items);
        Assert.All(completedResult.Items, task =>
        {
            Assert.Equal(uniqueTitle, task.Title);
            Assert.True(task.IsCompleted);
        });

        // Act: request incomplete tasks.
        var incompleteFilterResponse = await _client.GetAsync(
            $"/api/tasks?isCompleted=false&search={uniqueTitle}");

        Assert.Equal(
            HttpStatusCode.OK, incompleteFilterResponse.StatusCode);

        var incompleteResult =
            await incompleteFilterResponse.Content
                .ReadFromJsonAsync<PagedResponse<TaskItem>>();

        // Assert: only the incomplete task is returned.
        Assert.NotNull(incompleteResult);
        Assert.Single(incompleteResult.Items);
        Assert.All(incompleteResult.Items, task =>
        {
            Assert.Equal(uniqueTitle, task.Title);
            Assert.False(task.IsCompleted);
        });
    }

}
