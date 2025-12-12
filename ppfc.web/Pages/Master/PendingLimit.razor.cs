using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen;
using Radzen.Blazor;
using System.Globalization;
using System.Text.Json;
using static System.Net.WebRequestMethods;

namespace ppfc.web.Pages.Master
{
    public partial class PendingLimit
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;
        [Inject] UserContext UserContext { get; set; }

        private RadzenDataGrid<PendingLimitDto> grid;
        public List<BranchesDto> branches = new();
        public List<PendingLimitDto> pendingLimits = new();

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
                pendingLimits = await Http.GetFromJsonAsync<List<PendingLimitDto>>($"Master/GetPendingLimits/{companyId}");
                branches = await Http.GetFromJsonAsync<List<BranchesDto>>($"Master/GetBranchesForPendingLimit/{companyId}");
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

        public async Task EditPendingLimit(PendingLimitDto limit)
        {
            await grid.EditRow(limit);
            await Task.Yield();
        }

        public async Task CancelEdit(PendingLimitDto limit)
        {
            grid.CancelEditRow(limit);
            await Task.Yield();
        }

        public async Task AddNew()
        {
            var newLimit = new PendingLimitDto
            {
                CompanyId = companyId,
                IsNew = true
            };
            pendingLimits.Insert(0, newLimit);
            await grid.InsertRow(newLimit);
        }

        public async Task SavePending(PendingLimitDto pendingLimit)
        {
            if (pendingLimit.BranchId == 0)
            {
                Notifier.Warning("Validation Error", "Please select a Branch.");
                return;
            }
            if (pendingLimit.PendingLimitValue <= 0)
            {
                Notifier.Warning("Validation Error", "Pending Amount must be greater than zero.");
                return;
            }
            try
            {
                if (pendingLimit.PendingLimitId == 0)
                {
                    var response = await Http.PostAsJsonAsync("Master/AddPendingLimit", pendingLimit);
                    if (response.IsSuccessStatusCode)
                    {
                        Notifier.Success("Success", "Pending Limit saved successfully.");
                        await LoadData();
                    }
                    else
                    {
                        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                        string message = json.GetProperty("message").GetString();
                        Notifier.Error("Failed to add Pending Limit!", message);
                    }
                }
                else
                {
                    var response = await Http.PutAsJsonAsync("Master/UpdatePendingLimit", pendingLimit);
                    if (response.IsSuccessStatusCode)
                    {
                        Notifier.Success("Success", "Pending Limit updated successfully.");
                        await LoadData();
                    }
                    else
                    {
                        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                        string message = json.GetProperty("message").GetString();
                        Notifier.Error("Failed to update Pending Limit!", message);
                    }
                }

            }
            catch (Exception ex)
            {
                Notifier.Error("Exception", $"An error occurred: {ex.Message}");
            }
        }

        public async Task DeletePendingLimit(PendingLimitDto pendingLimit)
        {
            bool? confirmed = await DialogService.Confirm(
                $"Are you sure you want to delete this Pending Limit?",
                "Confirm Delete",
                new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
            );

            if (confirmed != true)
            {
                return;
            }

            try
            {
                var response = await Http.DeleteAsync($"Master/DeletePendingLimit/{pendingLimit.PendingLimitId}");
                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Success", "Pending Limit deleted successfully.");
                    pendingLimits.Remove(pendingLimit);
                    await grid.Reload();
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Failed to delete Pending Limit!", message);
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Exception", $"An error occurred: {ex.Message}");
            }
        }
    }
}
