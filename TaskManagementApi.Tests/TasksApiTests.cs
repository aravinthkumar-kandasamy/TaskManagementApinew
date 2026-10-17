
using System;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;
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
        var task = new TaskItem
        {
            Title = "AB"
        };

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
        // Arrange: create three tasks.
        for (var i = 1; i <= 3; i++)
        {
            var task = new TaskItem
            {
                Title = $"Pagination task {i}",
                Description = "Pagination integration test"
            };

            var createResponse =
                await _client.PostAsJsonAsync("/api/tasks", task);

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        }

        // Act
        var response =
            await _client.GetAsync("/api/tasks?page=1&pageSize=2");

        // Assert
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
    public async Task GetAll_WithSecondPage_ReturnsRemainingTasks()
    {
        // Arrange: create three tasks.
        for (var i = 1; i <= 3; i++)
        {
            var task = new TaskItem
            {
                Title = $"Second page task {i}"
            };

            var createResponse =
                await _client.PostAsJsonAsync("/api/tasks", task);

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        }

        // Act
        var response =
            await _client.GetAsync("/api/tasks?page=2&pageSize=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result =
            await response.Content.ReadFromJsonAsync<PagedResponse<TaskItem>>();

        Assert.NotNull(result);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(1, result.Items.Count);
        Assert.True(result.TotalPages >= 2);
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


    [Fact]
    public async Task GetAll_FilterByCompletion_ReturnsMatchingTasks()
    {
        var task = new TaskItem
        {
            Title = "PR4-filter-completed-unique",
            IsCompleted = true
        };

        var createResponse = await _client.PostAsJsonAsync(
            "/api/tasks", task);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var response = await _client.GetAsync(
            "/api/tasks?isCompleted=true&search=PR4-filter-completed-unique");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result =
            await response.Content.ReadFromJsonAsync<PagedResponse<TaskItem>>();

        Assert.NotNull(result);
        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item =>
        {
            Assert.True(item.IsCompleted);
            Assert.Contains(
                "PR4-filter-completed-unique",
                item.Title,
                StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task GetAll_SearchByTitle_ReturnsMatchingTasks()
    {
        var task = new TaskItem
        {
            Title = "PR4-search-unique-task"
        };

        var createResponse = await _client.PostAsJsonAsync(
            "/api/tasks", task);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var response = await _client.GetAsync(
            "/api/tasks?search=PR4-search-unique-task");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result =
            await response.Content.ReadFromJsonAsync<PagedResponse<TaskItem>>();

        Assert.NotNull(result);
        Assert.Contains(result.Items, item =>
            item.Title == "PR4-search-unique-task");
    }

    [Fact]
    public async Task GetAll_SortByTitleAscending_ReturnsSortedTasks()
    {
        var titles = new[]
        {
        "PR4-sort-unique-Beta",
        "PR4-sort-unique-Alpha"
    };

        foreach (var title in titles)
        {
            var response = await _client.PostAsJsonAsync(
                "/api/tasks",
                new TaskItem { Title = title });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var response = await _client.GetAsync(
            "/api/tasks?search=PR4-sort-unique-&sortBy=title&sortOrder=asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result =
            await response.Content.ReadFromJsonAsync<PagedResponse<TaskItem>>();

        Assert.NotNull(result);

        var actualTitles = result.Items
            .Select(item => item.Title)
            .ToList();

        Assert.Equal(
            new[]
            {
            "PR4-sort-unique-Alpha",
            "PR4-sort-unique-Beta"
            },
            actualTitles);
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
}
