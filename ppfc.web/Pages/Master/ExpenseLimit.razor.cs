using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen;
using Radzen.Blazor;
using System.Globalization;
using System.Text.Json;

namespace ppfc.web.Pages.Master
{
    public partial class ExpenseLimit
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;
        [Inject] UserContext UserContext { get; set; }

        private RadzenDataGrid<ExpenseLimitDto> grid;
        public List<BranchesDto> branches = new();
        public List<ExpenseLimitDto> expenseLimits = new();

        public bool IsLoading = true;
        public int companyId { get; set; }

        protected override async Task OnInitializedAsync()
        {
            companyId = UserContext.CompanyId;

            await LoadData();
        }

        private async Task LoadData()
        {
            IsLoading = true;
            try
            {
                expenseLimits = await Http.GetFromJsonAsync<List<ExpenseLimitDto>>($"Master/GetExpenseLimits/{companyId}");
                branches = await Http.GetFromJsonAsync<List<BranchesDto>>($"Master/GetBranchesForExpenseLimit/{companyId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load Branches: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public string FormatNumber(int value)
        {
            // Format with thousands separators and no decimals
            return value.ToString("N0", CultureInfo.CurrentCulture);
        }

        public async Task EditExpenseLimit(ExpenseLimitDto limit)
        {
            await grid.EditRow(limit);
            await Task.Yield();
        }

        public async Task CancelEdit(ExpenseLimitDto limit)
        {
            grid.CancelEditRow(limit);
            await Task.Yield();
        }

        public async Task AddNew() 
        {             
            var newLimit = new ExpenseLimitDto
            {
                CompanyId = companyId,
                IsNew = true
            };
            expenseLimits.Insert(0, newLimit);
            await grid.InsertRow(newLimit);
        }

        public async Task SaveExpense(ExpenseLimitDto expeneLimit) {           
            if (expeneLimit.BranchId == 0)
            {
                Notifier.Warning("Validation Error", "Please select a Branch.");
                return;
            }
            if(expeneLimit.ExpenseLimitValue <= 0)
            {
                Notifier.Warning("Validation Error", "Expense Amount must be greater than zero.");
                return;
            }
            try
            {
                if(expeneLimit.ExpenseLimitId == 0)
                {
                    var response = await Http.PostAsJsonAsync("Master/AddExpenseLimit", expeneLimit);
                    if (response.IsSuccessStatusCode)
                    {
                        Notifier.Success("Success", "Expense Limit saved successfully.");
                        await LoadData();
                    }
                    else
                    {
                        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                        string message = json.GetProperty("message").GetString();
                        Notifier.Error("Failed to add Expense Limit!", message);
                    }
                }
                else
                {
                    var response = await Http.PutAsJsonAsync("Master/UpdateExpenseLimit", expeneLimit);
                    if (response.IsSuccessStatusCode)
                    {
                        Notifier.Success("Success", "Expense Limit updated successfully.");
                        await LoadData();
                    }
                    else
                    {
                        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                        string message = json.GetProperty("message").GetString();
                        Notifier.Error("Failed to update Expense Limit!", message);
                    }
                }
                
            }
            catch (Exception ex)
            {
                Notifier.Error("Exception", $"An error occurred: {ex.Message}");
            }
        }

        public async Task DeleteExpenseLimit(ExpenseLimitDto expenseLimit)
        {
            bool? confirmed = await DialogService.Confirm(
                $"Are you sure you want to delete this Expense Limit?",
                "Confirm Delete",
                new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
            );

            if (confirmed != true)
            {
                return;
            }

            try
            {
                var response = await Http.DeleteAsync($"Master/DeleteExpenseLimit/{expenseLimit.ExpenseLimitId}");
                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Success", "Expense Limit deleted successfully.");
                    expenseLimits.Remove(expenseLimit);
                    await grid.Reload();
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Failed to delete Expense Limit!", message);
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Exception", $"An error occurred: {ex.Message}");
            }
        }
    }
}
