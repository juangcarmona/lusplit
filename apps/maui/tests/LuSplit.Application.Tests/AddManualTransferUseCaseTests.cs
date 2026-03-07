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

        await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(new AddManualTransferInput(
            GroupId: "g1",
            FromParticipantId: "p1",
            ToParticipantId: "p1",
            AmountMinor: 50)));
    }
}
