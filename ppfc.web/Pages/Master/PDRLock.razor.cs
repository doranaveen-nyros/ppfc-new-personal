using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen;
using Radzen.Blazor;
using System.Globalization;
using System.Text.Json;
using static System.Net.WebRequestMethods;

namespace ppfc.web.Pages.Master
{
    public partial class PDRLock
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;
        [Inject] UserContext UserContext { get; set; }

        private RadzenDataGrid<PDRLockDto> grid;
        public List<BranchesDto> branches = new();
        public List<PDRLockDto> pdrLocks = new();

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
                pdrLocks = await Http.GetFromJsonAsync<List<PDRLockDto>>($"Master/GetPDRLocks/{companyId}");
                branches = await Http.GetFromJsonAsync<List<BranchesDto>>($"Master/GetBranchesForPDRLock/{companyId}");
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

        public async Task EditPDRLockLimit(PDRLockDto limit)
        {
            await grid.EditRow(limit);
            await Task.Yield();
        }

        public async Task CancelEdit(PDRLockDto limit)
        {
            grid.CancelEditRow(limit);
            await Task.Yield();
        }

        public async Task AddNew()
        {
            var newLimit = new PDRLockDto
            {
                CompanyId = companyId,
                IsNew = true
            };
            pdrLocks.Insert(0, newLimit);
            await grid.InsertRow(newLimit);
        }

        public async Task SavePDRLock(PDRLockDto expeneLimit)
        {
            if (expeneLimit.BranchId == 0)
            {
                Notifier.Warning("Validation Error", "Please select a Branch.");
                return;
            }
            if (expeneLimit.LockAmount <= 0)
            {
                Notifier.Warning("Validation Error", "PDR Lock Amount must be greater than zero.");
                return;
            }
            try
            {
                if (expeneLimit.PDRLockId == 0)
                {
                    var response = await Http.PostAsJsonAsync("Master/AddPDRLock", expeneLimit);
                    if (response.IsSuccessStatusCode)
                    {
                        Notifier.Success("Success", "PDR Lock Amount saved successfully.");
                        await LoadData();
                    }
                    else
                    {
                        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                        string message = json.GetProperty("message").GetString();
                        Notifier.Error("Failed to add PDR Lock Amount!", message);
                    }
                }
                else
                {
                    var response = await Http.PutAsJsonAsync("Master/UpdatePDRLock", expeneLimit);
                    if (response.IsSuccessStatusCode)
                    {
                        Notifier.Success("Success", "PDR Lock Amount updated successfully.");
                        await LoadData();
                    }
                    else
                    {
                        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                        string message = json.GetProperty("message").GetString();
                        Notifier.Error("Failed to update PDR Lock Amount!", message);
                    }
                }

            }
            catch (Exception ex)
            {
                Notifier.Error("Exception", $"An error occurred: {ex.Message}");
            }
        }

        public async Task DeletePDRLock(PDRLockDto pdrLock)
        {
            bool? confirmed = await DialogService.Confirm(
                $"Are you sure you want to delete this PDR Lock?",
                "Confirm Delete",
                new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
            );

            if (confirmed != true)
            {
                return;
            }

            try
            {
                var response = await Http.DeleteAsync($"Master/DeletePDRLock/{pdrLock.PDRLockId}");
                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Success", "PDR Lock deleted successfully.");
                    pdrLocks.Remove(pdrLock);
                    await grid.Reload();
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Failed to delete PDR Lock!", message);
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Exception", $"An error occurred: {ex.Message}");
            }
        }
    }
}
