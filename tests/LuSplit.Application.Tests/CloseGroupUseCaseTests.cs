using LuSplit.Application.Groups.Commands;
using LuSplit.Application.Shared.Commands;
using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Domain.Groups;

namespace LuSplit.Application.Tests;

public sealed class CloseGroupUseCaseTests
{
    [Fact]
    public async Task ExecuteAsyncClosesAnExistingGroup()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));

        var useCase = new CloseGroupUseCase(repos);

        var result = await useCase.ExecuteAsync(new CloseGroupInput("g1"));

        Assert.True(result.Closed);
        Assert.True(repos.Groups.Single(group => group.Id == "g1").Closed);
    }

    [Fact]
    public async Task ExecuteAsyncFailsWhenGroupDoesNotExist()
    {
        var repos = new InMemoryQueryRepositories();
        var useCase = new CloseGroupUseCase(repos);

        var error = await Assert.ThrowsAsync<NotFoundError>(() => useCase.ExecuteAsync(new CloseGroupInput("g-missing")));

        Assert.Equal("Group not found: g-missing", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncRequiresGroupId()
    {
        var repos = new InMemoryQueryRepositories();
        var useCase = new CloseGroupUseCase(repos);

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new CloseGroupInput("  ")));

        Assert.Equal("groupId is required", error.Message);
    }
}
