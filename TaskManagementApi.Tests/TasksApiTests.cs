
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
}
