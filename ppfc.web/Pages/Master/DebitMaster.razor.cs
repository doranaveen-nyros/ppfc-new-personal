using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen;
using Radzen.Blazor;
using System.Text.Json;

namespace ppfc.web.Pages.Master
{
    public partial class DebitMaster
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;
        [Inject] UserContext UserContext { get; set; }

        private RadzenDataGrid<DebitDto> grid;
        public List<DebitDto> debits = new();

        public bool IsLoading = true;
        public int companyId { get; set; }

        protected override async Task OnInitializedAsync()
        {
            companyId = UserContext.CompanyId;

            await LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                debits = await Http.GetFromJsonAsync<List<DebitDto>>($"Master/GetDebits/{companyId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load Debit Accounts: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task EditDebit(DebitDto debit)
        {
            await grid.EditRow(debit);
            await Task.Yield();
        }

        public async Task CancelEdit(DebitDto debit)
        {
            grid.CancelEditRow(debit);
            StateHasChanged();
        }

        public async Task AddNew()
        {
            var newDebit = new DebitDto
            {
                CompanyId = companyId,
                DebitId = 0,
                DebitAccountName = string.Empty
            };
            debits.Insert(0, newDebit);
            await grid.InsertRow(newDebit);
        }

        public async Task SaveDebit(DebitDto debit)
        {
            if (string.IsNullOrWhiteSpace(debit.DebitAccountName))
            {
                Notifier.Warning("Debit Account Name cannot be empty.");
                return;
            }

            try
            {
                if (debit.DebitId == 0)
                {
                    var response = await Http.PostAsJsonAsync("Master/AddDebit", debit);
                    if (response.IsSuccessStatusCode)
                    {
                        await LoadData();
                        Notifier.Success("Debit account created successfully.");
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
                    var response = await Http.PutAsJsonAsync("Master/UpdateDebit", debit);
                    if (response.IsSuccessStatusCode)
                    {
                        await grid.UpdateRow(debit);
                        Notifier.Success("Debit account updated successfully.");
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
                Notifier.Error($"An error occurred: {ex.Message}");

            }
        }

        public async Task DeleteDebit(DebitDto debit)
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
                var response = await Http.DeleteAsync($"Master/DeleteDebit/{debit.DebitId}");
                if (response.IsSuccessStatusCode)
                {
                    debits.Remove(debit);
                    await grid.Reload();
                    Notifier.Success("Debit account deleted successfully.");
                }
                else
                {
                    Notifier.Error("Failed to delete debit account.");
                }
            }
            catch (Exception ex)
            {
                Notifier.Error($"An error occurred: {ex.Message}");
            }
        }
    }
}