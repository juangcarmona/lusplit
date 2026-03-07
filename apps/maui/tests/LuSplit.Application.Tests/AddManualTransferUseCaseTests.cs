using LuSplit.Application.Commands;
using LuSplit.Application.Errors;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Domain.Entities;

namespace LuSplit.Application.Tests;

public sealed class AddManualTransferUseCaseTests
{
    [Fact]
    public async Task ExecuteAsyncStoresManualTransfer()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.Participants.Add(new Participant("p1", "g1", "u1", "P1", ConsumptionCategory.Full));
        repos.Participants.Add(new Participant("p2", "g1", "u2", "P2", ConsumptionCategory.Full));

        var useCase = new AddManualTransferUseCase(
            repos,
            repos,
            repos,
            new SequentialIdGenerator(),
            new FixedClock("2026-01-01T00:00:00.000Z"));

        var result = await useCase.ExecuteAsync(new AddManualTransferInput(
            GroupId: "g1",
            FromParticipantId: "p2",
            ToParticipantId: "p1",
            AmountMinor: 50));

        Assert.Equal("id-1", result.Id);
        Assert.Equal("MANUAL", result.Type);
    }

    [Fact]
    public async Task ExecuteAsyncValidatesDifferentParticipants()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.Participants.Add(new Participant("p1", "g1", "u1", "P1", ConsumptionCategory.Full));

        var useCase = new AddManualTransferUseCase(
            repos,
            repos,
            repos,
            new SequentialIdGenerator(),
            new FixedClock("2026-01-01T00:00:00.000Z"));

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new AddManualTransferInput(
            GroupId: "g1",
            FromParticipantId: "p1",
            ToParticipantId: "p1",
            AmountMinor: 50)));

        Assert.Equal("fromParticipantId and toParticipantId must be different", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncRequiresGroupId()
    {
        var repos = new InMemoryQueryRepositories();
        var useCase = new AddManualTransferUseCase(
            repos,
            repos,
            repos,
            new SequentialIdGenerator(),
            new FixedClock("2026-01-01T00:00:00.000Z"));

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new AddManualTransferInput(
            GroupId: "  ",
            FromParticipantId: "p1",
            ToParticipantId: "p2",
            AmountMinor: 50)));

        Assert.Equal("groupId is required", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncRejectsInvalidIsoDate()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.Participants.Add(new Participant("p1", "g1", "u1", "P1", ConsumptionCategory.Full));
        repos.Participants.Add(new Participant("p2", "g1", "u2", "P2", ConsumptionCategory.Full));

        var useCase = new AddManualTransferUseCase(
            repos,
            repos,
            repos,
            new SequentialIdGenerator(),
            new FixedClock("2026-01-01T00:00:00.000Z"));

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new AddManualTransferInput(
            GroupId: "g1",
            FromParticipantId: "p1",
            ToParticipantId: "p2",
            AmountMinor: 50,
            Date: "not-a-date")));

        Assert.Equal("date must be a valid ISO date", error.Message);
    }

    [Fact]
    public async Task ExecuteAsyncValidatesParticipantsBelongToGroup()
    {
        var repos = new InMemoryQueryRepositories();
        repos.Groups.Add(new Group("g1", "USD", false));
        repos.Participants.Add(new Participant("p1", "g1", "u1", "P1", ConsumptionCategory.Full));

        var useCase = new AddManualTransferUseCase(
            repos,
            repos,
            repos,
            new SequentialIdGenerator(),
            new FixedClock("2026-01-01T00:00:00.000Z"));

        var error = await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new AddManualTransferInput(
            GroupId: "g1",
            FromParticipantId: "p1",
            ToParticipantId: "p-missing",
            AmountMinor: 50)));

        Assert.Equal("Transfer participants must belong to group g1", error.Message);
    }
}
