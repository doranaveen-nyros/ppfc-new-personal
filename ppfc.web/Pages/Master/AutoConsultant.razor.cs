using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.VisualBasic;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen;
using Radzen.Blazor;
using System.Text.Json;

namespace ppfc.web.Pages.Master
{
    public partial class AutoConsultant
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;
        [Inject] UserContext UserContext { get; set; }
        [Inject] IJSRuntime JS { get; set; }


        private RadzenDataGrid<AutoConsultantDto> grid;
        public List<BranchesDto> branches = new();
        public List<AutoConsultantDto> autoConsultants = new();

        public bool isAddModalOpen = false;
        AutoConsultantDto newConsultant = new();

        public bool IsLoading = true;
        public int? IsDelLoading = null;
        public bool IsSubmitting = false;

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
                autoConsultants = await Http.GetFromJsonAsync<List<AutoConsultantDto>>($"Master/GetAutoConsultants/{companyId}");
                branches = await Http.GetFromJsonAsync<List<BranchesDto>>($"Master/GetBranches/{companyId}");
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

        // modal handlers
        public void OpenAddModal()
        {
            newConsultant = new AutoConsultantDto();
            isAddModalOpen = true;
        }

        public void CloseAddModal()
        {
            isAddModalOpen = false;
        }

        void EditRow(AutoConsultantDto item)
        {
            grid!.EditRow(item);
        }

        void CancelEdit(AutoConsultantDto item)
        {
            if (item.IsNew)
            {
                autoConsultants.Remove(item);
                grid!.Reload();
            }
            else
            {
                grid!.CancelEditRow(item);
            }
        }

        public async Task OnAddSubmit()
        {
            if (!ValidateConsultant(newConsultant))
            {
                return;
            }

            IsSubmitting = true;

            try
            {
                var response = await Http.PostAsJsonAsync("Master/AddAutoConsultant", newConsultant);
                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Auto Consultant added successfully.");
                    await LoadData();
                    CloseAddModal();
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Failed to add Auto Consultant!", message);
                }
            }
            catch (Exception ex)
            {
                Notifier.Error($"Error: {ex.Message}");
            }

            IsSubmitting = false;
        }

        private bool ValidateConsultant(AutoConsultantDto autoConsultant)
        {
            if (string.IsNullOrWhiteSpace(autoConsultant.AutoConsultantName))
            {
                Notifier.Warning("Validation", "Auto Consultant Name is required.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(autoConsultant.PhoneNo)) 
            { 
                Notifier.Warning("Validation", "Phone Number is required.");
                return false;
            }
            if(autoConsultant.Limit < 0)
            {
                Notifier.Warning("Validation", "Limit cannot be less than 0.");
                return false;
            }
            if (autoConsultant.BranchId <= 0)
            {
                Notifier.Warning("Validation", "Branch selection is required.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(autoConsultant.UPIType))
            {
                Notifier.Warning("Validation", "UPI Type is required.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(autoConsultant.UPIName))
            {
                Notifier.Warning("Validation", "UPI Name is required.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(autoConsultant.Bank))
            {
                Notifier.Warning("Validation", "Bank Name is required.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(autoConsultant.AccountNumber))
            {
                Notifier.Warning("Validation", "Account Number is required.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(autoConsultant.AccountName))
            {
                Notifier.Warning("Validation", "Account Name is required.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(autoConsultant.IFSCCode))
            {
                Notifier.Warning("Validation", "IFSC Code is required.");
                return false;
            }
            return true;
        }

        public async Task UpdateConsultant(AutoConsultantDto autoConsultant) 
        { 
            if(!ValidateConsultant(autoConsultant))
            {
                return;
            }

            try
            {
                var response = await Http.PutAsJsonAsync("Master/UpdateAutoConsultant", autoConsultant);
                if (response.IsSuccessStatusCode)
                {
                    await grid.UpdateRow(autoConsultant);
                    Notifier.Success("Auto Consultant updated successfully.");
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Failed to update Auto Consultant!", message);
                }
            }
            catch (Exception ex)
            {
                Notifier.Error($"Error: {ex.Message}");
            }
        }

        public async Task DeleteConsultant(AutoConsultantDto autoConsultant)
        {
            IsDelLoading = autoConsultant.AutoConsultantId;

            bool? confirmed = await DialogService.Confirm(
                $"Are you sure you want to delete this Auto Consultant?",
                "Confirm Delete",
                new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
            );

            if (confirmed != true)
            {
                IsDelLoading = null;
                return;
            }

            try
            {
                var response = await Http.DeleteAsync($"Master/DeleteAutoConsultant/{autoConsultant.AutoConsultantId}");
                if (response.IsSuccessStatusCode)
                {
                    autoConsultants.Remove(autoConsultant);
                    await grid.Reload();
                    Notifier.Success("Auto Consultant deleted successfully.");
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Failed to delete Auto Consultant!", message);
                }
            }
            catch (Exception ex)
            {
                Notifier.Error($"Error: {ex.Message}");
            }
            IsDelLoading = null;
        }

        private async Task ToggleLockAsync(AutoConsultantDto item, bool newValue)
        {
            try
            {
                // Option 1: Call the dedicated ToggleLock API
                var response = await Http.PutAsync($"Master/ToggleAutoConsultantLock/{item.AutoConsultantId}", null);

                if (response.IsSuccessStatusCode)
                {
                    item.Lock = newValue;
                    Notifier.Success("Lock Updated Successfully", $"Lock updated for branch {item.AutoConsultantName}.");
                }
                else
                {
                    // Revert toggle if API fails
                    item.Lock = !newValue;
                    var msg = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Failed to update lock: {msg}");
                    Notifier.Error("Error updating lock");
                }
            }
            catch (Exception ex)
            {
                item.Lock = !newValue; // revert
                Console.WriteLine($"Error updating lock: {ex.Message}");
                Notifier.Error("Error updating lock");
            }
        }

        private async Task ExportToCSV()
        {
            if (grid == null || grid.PagedView == null || !grid.PagedView.Any())
            {
                Notifier.Warning("No Data", "No records to export.");
                return;
            }

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("AutoConsultant Name,PhoneNo,Limit,BranchName,Bank,AccountNumber,AccountName,IFSCCode,UPIName,UPIType,Lock Status");

            foreach (var c in grid.PagedView)
            {
                var cleanName = c.AutoConsultantName?.Replace("\"", "\"\"");
                var branchName = branches.FirstOrDefault(b => b.BranchId == c.BranchId)?.BranchName ?? "";
                var lockStatus = c.Lock ? "Locked" : "Unlocked";

                csv.AppendLine($"\"{cleanName}\",{c.PhoneNo},{c.Limit},\"{branchName}\",{c.Bank},{c.AccountNumber},{c.AccountName},{c.IFSCCode},{c.UPIName},{c.UPIType},{lockStatus}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"AutoConsultants_{DateTime.Now:dd-MMM-yyyy_hh-mm-tt}.csv";

            await JS.InvokeVoidAsync("downloadFile", fileName, "text/csv", Convert.ToBase64String(bytes));
        }



    }
}
