using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen;
using Radzen.Blazor;
using System.Diagnostics;
using System.Text.Json;

namespace ppfc.web.Pages.Master
{
    public partial class ExpenseMaster
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;

        private RadzenDataGrid<ExpenseDto> grid;
        public List<ExpenseDto> expenses = new();

        public bool IsLoading = true;

        protected override async Task OnInitializedAsync()
        {
            await LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                expenses = await Http.GetFromJsonAsync<List<ExpenseDto>>($"Master/GetExpenses");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load Expense Accounts: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task EditExpense(ExpenseDto expense)
        {
            await grid.EditRow(expense);
            await Task.Yield();
        }

        public async Task CancelEdit(ExpenseDto expense)
        {
            grid.CancelEditRow(expense);
            StateHasChanged();
        }

        public async Task AddNew()
        {
            var newExpense = new ExpenseDto { IsNew = true };
            expenses.Insert(0, newExpense);
            await grid.InsertRow(newExpense);
        }

        public async Task SaveExpense(ExpenseDto expense)
        {
            if(string.IsNullOrWhiteSpace(expense.ExpenseName))
            {
                Notifier.Warning("Expense Name is required.");
                return;
            }

            try
            {
                if(expense.ExpenseId == 0)
                {
                    var response = await Http.PostAsJsonAsync("Master/AddExpense", expense);
                    if (response.IsSuccessStatusCode)
                    {
                        await LoadData();
                        Notifier.Success("Expense account saved successfully.");
                    }
                    else
                    {
                        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                        string message = json.GetProperty("message").GetString();
                        Notifier.Error("Failed to Create!", message);
                    }
                }
                else
                {
                    var response = await Http.PutAsJsonAsync("Master/UpdateExpense", expense);
                    if (response.IsSuccessStatusCode)
                    {
                        await grid.UpdateRow(expense);
                        Notifier.Success("Expense account saved successfully.");
                    }
                    else
                    {
                        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                        string message = json.GetProperty("message").GetString();
                        Notifier.Error("Failed to Update!", message);
                    }
                }
              
            }
            catch (Exception ex)
            {
                Notifier.Error($"Exception: {ex.Message}");
            }
        }

        public async Task DeleteExpense(ExpenseDto expense)
        {
            bool? confirmed = await DialogService.Confirm(
                                        $"Are you sure you want to delete this Expense?",
                                        "Confirm Delete",
                                        new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
                                    );

            if (confirmed != true)
            {
                return;
            }

            try
            {
                var response = await Http.DeleteAsync($"Master/DeleteExpense/{expense.ExpenseId}");
                if (response.IsSuccessStatusCode)
                {                    
                    expenses.Remove(expense);
                    await grid.Reload();
                    Notifier.Success("Expense account deleted successfully.");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Notifier.Error($"Error deleting expense account: {error}");
                }
            }
            catch (Exception ex)
            {
                Notifier.Error($"Exception: {ex.Message}");
            }
        }
    }
}
