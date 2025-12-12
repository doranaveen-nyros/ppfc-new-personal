using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen;
using Radzen.Blazor;
using System.Text.Json;

namespace ppfc.web.Pages.Master
{
    public partial class CreditMaster
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;
        [Inject] UserContext UserContext { get; set; }

        private RadzenDataGrid<CreditDto> grid;
        public List<CreditDto> credits = new();

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
                credits = await Http.GetFromJsonAsync<List<CreditDto>>($"Master/GetCredits/{companyId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load Credit Accounts: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task EditCredit(CreditDto credit)
        {
            await grid.EditRow(credit);
            await Task.Yield();
        }

        public async Task CancelEdit(CreditDto credit)
        {
            grid.CancelEditRow(credit);
            StateHasChanged();
        }

        public async Task AddNew()
        {
            var newCredit = new CreditDto
            {
                CompanyId = companyId,
                CreditId = 0,
                CreditAccountName = string.Empty
            };
            credits.Insert(0, newCredit);
            await grid.InsertRow(newCredit);
        }

        public async Task SaveCredit(CreditDto credit)
        {
            if (string.IsNullOrWhiteSpace(credit.CreditAccountName))
             {
                Notifier.Warning("Credit Account Name cannot be empty.");
                return;
            }

            try
            {
                if(credit.CreditId == 0)
                {
                    var response = await Http.PostAsJsonAsync("Master/AddCredit", credit);
                    if (response.IsSuccessStatusCode)
                    {
                        await LoadData();
                        Notifier.Success("Credit Account added successfully.");
                    }
                    else
                    {
                        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                        string message = json.GetProperty("message").GetString();
                        Notifier.Error("Failed to Add!", message);
                    }
                }
                else
                {
                    var response = await Http.PutAsJsonAsync("Master/UpdateCredit", credit);
                    if (response.IsSuccessStatusCode)
                    {
                        await grid.UpdateRow(credit);
                        Notifier.Success("Credit Account updated successfully.");
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
                Console.WriteLine($"❌ Error saving Credit Account: {ex.Message}");
                Notifier.Error("An error occurred while saving the credit.");
            }
        }

        public async Task DeleteCredit(CreditDto credit)
        {
            bool? confirmed = await DialogService.Confirm(
                $"Are you sure you want to delete this Credit Account?",
                "Confirm Delete",
                new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
            );

            if (confirmed != true)
            {
                return;
            }

            try
            {
                IsLoading = true;
                var response = await Http.DeleteAsync($"Master/DeleteCredit/{credit.CreditId}");
                if (response.IsSuccessStatusCode)
                {
                    credits.Remove(credit);
                    await grid.Reload();
                    Notifier.Success("Credit Account deleted successfully.");
                }
                else
                {
                    Notifier.Error("Failed to delete credit account.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error deleting Credit Account: {ex.Message}");
                Notifier.Error("An error occurred while deleting the credit.");
            }
            finally
            {
                IsLoading = false;
            }
        }

    }
}
