using LuSplit.Application.Models;

namespace LuSplit.App.Services;

public interface IRecordPaymentDataService
{
    Task<GroupOverviewModel> GetOverviewAsync();
    Task AddPaymentAsync(string fromParticipantId, string toParticipantId, long amountMinor, DateTime date);
}
