using LuSplit.Application.Commands;
using LuSplit.Application.Models;
using LuSplit.Application.Queries;
using LuSplit.Application.Tests.Fakes;
using LuSplit.Domain.Entities;
using LuSplit.Domain.Split;

namespace LuSplit.Application.Tests;

public sealed class ApplicationFlowParityTests
{
    [Fact]
    public async Task EndToEndFlowComputesDeterministicBalancesAndSettlementForParticipantAndOwnerModes()
    {
        var repos = new InMemoryQueryRepositories();
        var idGenerator = new SequentialIdGenerator();

        var createGroup = new CreateGroupUseCase(repos, idGenerator);
        var createEconomicUnit = new CreateEconomicUnitUseCase(repos, repos, idGenerator);
        var createParticipant = new CreateParticipantUseCase(repos, repos, repos, idGenerator);
        var addExpense = new AddExpenseUseCase(
            repos,
            repos,
            repos,
            idGenerator,
            new FixedClock("2026-01-01T00:00:00.000Z"));
        var getBalancesByParticipant = new GetBalancesByParticipantUseCase(repos, repos, repos, repos);
        var getBalancesByEconomicUnitOwner = new GetBalancesByEconomicUnitOwnerUseCase(repos, repos, repos, repos, repos);
        var getSettlementPlan = new GetSettlementPlanUseCase(repos, repos, repos, repos, repos);

        var group = await createGroup.ExecuteAsync(new CreateGroupInput("USD"));
        var unit1 = await createEconomicUnit.ExecuteAsync(new CreateEconomicUnitInput(group.Id, "id-4", "Unit 1"));
        var unit2 = await createEconomicUnit.ExecuteAsync(new CreateEconomicUnitInput(group.Id, "id-5", "Unit 2"));

        var p1 = await createParticipant.ExecuteAsync(new CreateParticipantInput(group.Id, unit1.Id, "Alice", ConsumptionCategory.Full));
        var p2 = await createParticipant.ExecuteAsync(new CreateParticipantInput(group.Id, unit2.Id, "Bob", ConsumptionCategory.Full));
        var p3 = await createParticipant.ExecuteAsync(new CreateParticipantInput(group.Id, unit2.Id, "Carol", ConsumptionCategory.Half));

        await addExpense.ExecuteAsync(new AddExpenseInput(
            GroupId: group.Id,
            Title: "Dinner",
            PaidByParticipantId: p1.Id,
            AmountMinor: 900,
            SplitDefinition: new SplitDefinition(new SplitComponent[]
            {
                new RemainderSplitComponent(new[] { p1.Id, p2.Id, p3.Id }, RemainderMode.Equal)
            })));

        await addExpense.ExecuteAsync(new AddExpenseInput(
            GroupId: group.Id,
            Title: "Groceries",
            PaidByParticipantId: p2.Id,
            AmountMinor: 600,
            SplitDefinition: new SplitDefinition(new SplitComponent[]
            {
                new FixedSplitComponent(new Dictionary<string, long>
                {
                    [p1.Id] = 100
                }),
                new RemainderSplitComponent(new[] { p2.Id, p3.Id }, RemainderMode.Equal)
            })));

        var balancesByParticipant = await getBalancesByParticipant.ExecuteAsync(group.Id);
        var balancesByOwner = await getBalancesByEconomicUnitOwner.ExecuteAsync(group.Id);
        var participantSettlement = await getSettlementPlan.ExecuteAsync(group.Id, SettlementMode.Participant);
        var ownerSettlement = await getSettlementPlan.ExecuteAsync(group.Id, SettlementMode.EconomicUnitOwner);

        Assert.Equal(
            new[]
            {
                new BalanceModel(p1.Id, 500),
                new BalanceModel(p2.Id, 50),
                new BalanceModel(p3.Id, -550)
            },
            balancesByParticipant);

        Assert.Equal(
            new[]
            {
                new BalanceModel(p1.Id, 500),
                new BalanceModel(p2.Id, -500)
            },
            balancesByOwner);

        Assert.Equal(
            new[]
            {
                new SettlementTransferModel(p3.Id, p1.Id, 500),
                new SettlementTransferModel(p3.Id, p2.Id, 50)
            },
            participantSettlement.Transfers);

        Assert.Equal(
            new[]
            {
                new SettlementTransferModel(p2.Id, p1.Id, 500)
            },
            ownerSettlement.Transfers);
    }

    [Fact]
    public async Task ParticipantSettlementKeepsFairnessWhenMultiplePeoplePayInsideSameResponsibility()
    {
        var repos = new InMemoryQueryRepositories();
        var idGenerator = new SequentialIdGenerator();

        var createGroup = new CreateGroupUseCase(repos, idGenerator);
        var createEconomicUnit = new CreateEconomicUnitUseCase(repos, repos, idGenerator);
        var createParticipant = new CreateParticipantUseCase(repos, repos, repos, idGenerator);
        var addExpense = new AddExpenseUseCase(
            repos,
            repos,
            repos,
            idGenerator,
            new FixedClock("2026-01-01T00:00:00.000Z"));
        var getSettlementPlan = new GetSettlementPlanUseCase(repos, repos, repos, repos, repos);

        var group = await createGroup.ExecuteAsync(new CreateGroupInput("USD"));
        var sharedUnit = await createEconomicUnit.ExecuteAsync(new CreateEconomicUnitInput(group.Id, "id-3", "Shared"));

        var juan = await createParticipant.ExecuteAsync(new CreateParticipantInput(group.Id, sharedUnit.Id, "Juan", ConsumptionCategory.Full));
        var martina = await createParticipant.ExecuteAsync(new CreateParticipantInput(group.Id, sharedUnit.Id, "Martina", ConsumptionCategory.Half));
        var lucas = await createParticipant.ExecuteAsync(new CreateParticipantInput(group.Id, sharedUnit.Id, "Lucas", ConsumptionCategory.Half));
        var independentUnit = await createEconomicUnit.ExecuteAsync(new CreateEconomicUnitInput(group.Id, "id-7", "Independent"));
        var marta = await createParticipant.ExecuteAsync(new CreateParticipantInput(group.Id, independentUnit.Id, "Marta", ConsumptionCategory.Full));

        await addExpense.ExecuteAsync(new AddExpenseInput(
            GroupId: group.Id,
            Title: "Lunch",
            PaidByParticipantId: marta.Id,
            AmountMinor: 9000,
            SplitDefinition: new SplitDefinition(new SplitComponent[]
            {
                new RemainderSplitComponent(new[] { juan.Id, martina.Id, lucas.Id, marta.Id }, RemainderMode.Equal)
            })));

        await addExpense.ExecuteAsync(new AddExpenseInput(
            GroupId: group.Id,
            Title: "Snacks",
            PaidByParticipantId: juan.Id,
            AmountMinor: 2000,
            SplitDefinition: new SplitDefinition(new SplitComponent[]
            {
                new RemainderSplitComponent(new[] { juan.Id, martina.Id, lucas.Id, marta.Id }, RemainderMode.Equal)
            })));

        var participantPlan = await getSettlementPlan.ExecuteAsync(group.Id, SettlementMode.Participant);
        var ownerPlan = await getSettlementPlan.ExecuteAsync(group.Id, SettlementMode.EconomicUnitOwner);

        Assert.Contains(new SettlementTransferModel(lucas.Id, marta.Id, 2750), participantPlan.Transfers);
        Assert.Contains(new SettlementTransferModel(martina.Id, marta.Id, 2750), participantPlan.Transfers);
        Assert.Contains(new SettlementTransferModel(juan.Id, marta.Id, 750), participantPlan.Transfers);

        Assert.Single(ownerPlan.Transfers);
        Assert.Equal(new SettlementTransferModel(juan.Id, marta.Id, 6250), ownerPlan.Transfers[0]);
    }
}
