using LuSplit.Application.Commands;
using LuSplit.Application.Errors;
using LuSplit.Application.Tests.Fakes;

namespace LuSplit.Application.Tests;

public sealed class CreateGroupUseCaseTests
{
    [Fact]
    public async Task ExecuteAsyncCreatesAnOpenGroup()
    {
        var repos = new InMemoryQueryRepositories();
        var useCase = new CreateGroupUseCase(repos, new SequentialIdGenerator());

        var created = await useCase.ExecuteAsync(new CreateGroupInput("EUR"));

        Assert.Equal("EUR", created.Currency);
        Assert.False(created.Closed);
        Assert.Equal("id-1", created.Id);
    }

    [Fact]
    public async Task ExecuteAsyncValidatesCurrency()
    {
        var repos = new InMemoryQueryRepositories();
        var useCase = new CreateGroupUseCase(repos, new SequentialIdGenerator());

        await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new CreateGroupInput(string.Empty)));
    }
}
