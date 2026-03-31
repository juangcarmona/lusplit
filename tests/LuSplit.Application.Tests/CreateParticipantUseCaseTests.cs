using LuSplit.Application.Groups.Commands;
using LuSplit.Application.Shared.Commands;
using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Domain.Groups;

namespace LuSplit.Application.Tests;

public sealed class CreateParticipantUseCaseTests
{
    [Fact]
    public async Task ExecuteAsyncCreatesParticipantInExistingEconomicUnit()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.EconomicUnits.Add(new EconomicUnit("u1", "g1", "p1"));

        var useCase = new CreateParticipantUseCase(repos, repos, repos, new SequentialIdGenerator());

        var result = await useCase.ExecuteAsync(new CreateParticipantInput(
            GroupId: "g1",
            EconomicUnitId: "u1",
            Name: "Alice",
            ConsumptionCategory: ConsumptionCategory.Full));

        Assert.Equal("p1", result.Id);
        Assert.Equal("Alice", result.Name);
    }

    [Fact]
    public async Task ExecuteAsyncGeneratesNewIdAfterOwnerParticipantExistsInUnit()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.EconomicUnits.Add(new EconomicUnit("u1", "g1", "p1"));

        var useCase = new CreateParticipantUseCase(repos, repos, repos, new SequentialIdGenerator());

        var owner = await useCase.ExecuteAsync(new CreateParticipantInput("g1", "u1", "Alice", ConsumptionCategory.Full));
        var another = await useCase.ExecuteAsync(new CreateParticipantInput("g1", "u1", "Bob", ConsumptionCategory.Full));

        Assert.Equal("p1", owner.Id);
        Assert.Equal("id-1", another.Id);
    }

    [Fact]
    public async Task ExecuteAsyncValidatesCustomWeightForCustomCategory()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.EconomicUnits.Add(new EconomicUnit("u1", "g1", "p1"));

        var useCase = new CreateParticipantUseCase(repos, repos, repos, new SequentialIdGenerator());

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new CreateParticipantInput(
            GroupId: "g1",
            EconomicUnitId: "u1",
            Name: "Alice",
            ConsumptionCategory: ConsumptionCategory.Custom)));

        Assert.Equal("customConsumptionWeight is required for CUSTOM consumptionCategory", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncRequiresName()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.EconomicUnits.Add(new EconomicUnit("u1", "g1", "p1"));

        var useCase = new CreateParticipantUseCase(repos, repos, repos, new SequentialIdGenerator());

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new CreateParticipantInput(
            GroupId: "g1",
            EconomicUnitId: "u1",
            Name: "  ",
            ConsumptionCategory: ConsumptionCategory.Full)));

        Assert.Equal("name is required", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncErrorsWhenEconomicUnitIsOutsideGroup()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.EconomicUnits.Add(new EconomicUnit("u1", "g2", "p1"));

        var useCase = new CreateParticipantUseCase(repos, repos, repos, new SequentialIdGenerator());

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new CreateParticipantInput(
            GroupId: "g1",
            EconomicUnitId: "u1",
            Name: "Alice",
            ConsumptionCategory: ConsumptionCategory.Full)));

        Assert.Equal("Economic unit is not in group g1", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncErrorsWhenGroupDoesNotExist()
    {
        var repos = new InMemoryQueryRepositories();
        var useCase = new CreateParticipantUseCase(repos, repos, repos, new SequentialIdGenerator());

        var error = await Assert.ThrowsAsync<NotFoundError>(() => useCase.ExecuteAsync(new CreateParticipantInput(
            GroupId: "missing",
            EconomicUnitId: "u1",
            Name: "Alice",
            ConsumptionCategory: ConsumptionCategory.Full)));

        Assert.Equal("Group not found: missing", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncThrowsWhenIdGenerationIsExhausted()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.EconomicUnits.Add(new EconomicUnit("u1", "g1", "p1"));
        // Pre-seed the owner participant so the generator must produce a new unique ID.
        repos.Participants.Add(new Participant("p1", "g1", "u1", "Alice", ConsumptionCategory.Full));

        // Generator always returns "p1" which is already taken → exhausts all 100 attempts.
        var useCase = new CreateParticipantUseCase(repos, repos, repos, new CollisionIdGenerator("p1"));

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new CreateParticipantInput(
            GroupId: "g1",
            EconomicUnitId: "u1",
            Name: "Bob",
            ConsumptionCategory: ConsumptionCategory.Full)));

        Assert.Equal("Unable to generate a unique participant id", error.Message);
    }
}
