// Global using aliases to bridge old flat namespace structure to the Features/ layout.
// These allow existing tests to continue using their original `using` directives
// without modification while the source has been reorganized.

// LuSplit.App.Pages → Feature namespaces
global using LuSplit.App.Features.Activity;
global using LuSplit.App.Features.Activity.Activity;
global using LuSplit.App.Features.Expenses.AddExpense;
global using LuSplit.App.Features.Expenses.ExpenseDetails;
global using LuSplit.App.Features.Expenses.Shared;
global using LuSplit.App.Features.Groups.ArchivedGroups;
global using LuSplit.App.Features.Groups.ArchivedGroupView;
global using LuSplit.App.Features.Groups.CreateGroup;
global using LuSplit.App.Features.Groups.GroupDetails;
global using LuSplit.App.Features.Groups.GroupSwitcher;
global using LuSplit.App.Features.Groups.GroupTimeline;
global using LuSplit.App.Features.Home.Home;
global using LuSplit.App.Features.Payments.RecordPayment;
global using LuSplit.App.Features.Settings.Settings;
global using LuSplit.App.Features.SharedGroups;
global using LuSplit.App.Resources.Localization;

// LuSplit.App.Services → Persistence/Presentation/Formatting namespaces
global using LuSplit.App.Services.Errors;
global using LuSplit.App.Services.Export;
global using LuSplit.App.Services.Formatting;
global using LuSplit.App.Services.Localization;
global using LuSplit.App.Services.Persistence;
global using LuSplit.App.Services.Presentation;
global using LuSplit.App.Services.Settings;

// Also expose Features.Home (non-nested) in case any source files reference it
global using LuSplit.App.Features.Home;

// LuSplit.Application.Models → Application domain model namespaces
global using LuSplit.Application.Expenses.Models;
global using LuSplit.Application.Groups.Models;
global using LuSplit.Application.Payments.Models;
global using LuSplit.Application.Shared.Ports;
