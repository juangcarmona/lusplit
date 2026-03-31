using LuSplit.Application.Groups.Models;

namespace LuSplit.App.Features.Payments.RecordPayment;

public interface IRecordPaymentDataService
{
    Task<GroupOverviewModel> GetOverviewAsync();
    Task AddPaymentAsync(string fromParticipantId, string toParticipantId, long amountMinor, DateTime date);
}
