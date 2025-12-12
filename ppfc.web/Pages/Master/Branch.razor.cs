using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Diagnostics;
using ppfc.DTO;
using ppfc.web.Helpers;
using ppfc.web.Pages.Admin;
using Radzen;
using Radzen.Blazor;
using System.Text.Json;

namespace ppfc.web.Pages.Master
{
    public partial class Branch
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;
        [Inject] UserContext UserContext { get; set; }

        private RadzenDataGrid<BranchesDto> grid;
        public List<BranchesDto> branches = new();

        public bool IsLoading = true;
        public int? IsDelLoading = null;
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

        public async Task EditBranch(BranchesDto branch)
        {
            await grid.EditRow(branch);
            await Task.Yield();
        }

        public async Task CancelEdit(BranchesDto branch)
        {
            grid.CancelEditRow(branch);
            StateHasChanged();
        }

        public async Task AddNew()
        {
            var newBranch = new BranchesDto
            {
                BranchId = 0,
                CompanyId = companyId,
                BranchName = string.Empty,
                LockCapital = false
            };

            branches.Insert(0, newBranch);
            await grid.InsertRow(newBranch);
        }

        public async Task SaveBranch(BranchesDto branch)
        {
            if(string.IsNullOrWhiteSpace(branch.BranchName))
            {
                Notifier.Warning("Validation Error", "Enter Branch Name.");
                return;
            }

            if(branch.BranchId == 0)
            {
                // New Branch
                try
                {
                    var response = await Http.PostAsJsonAsync("Master/AddBranch", branch);
                    if (response.IsSuccessStatusCode)
                    {
                        await LoadData();
                        Notifier.Success("Added Successfully", "Branch has been added successfully.");
                    }
                    else
                    {
                        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                        string message = json.GetProperty("message").GetString();
                        Notifier.Error("Failed to Add!", message);
                    }
                }
                catch (Exception ex)
                {
                    Notifier.Error("Add Failed", $"An error occurred while adding the branch: {ex.Message}");
                }
                return;
            }
            else
            {
                // Existing Branch
                try
                {
                    var response = await Http.PutAsJsonAsync($"Master/UpdateBranch", branch);
                    if (response.IsSuccessStatusCode)
                    {
                        await grid.UpdateRow(branch);
                        Notifier.Success("Updated Successfully", "Branch has been updated successfully.");
                    }
                    else
                    {
                        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                        string message = json.GetProperty("message").GetString();
                        Notifier.Error("Failed to Update!", message);
                    }
                }
                catch (Exception ex)
                {
                    Notifier.Error("Update Failed", $"An error occurred while updating the branch: {ex.Message}");
                }
            }

        }

        public async Task DeleteBranch(BranchesDto branch)
        {
            IsDelLoading = branch.BranchId;

            bool? confirmed = await DialogService.Confirm(
                $"Are you sure you want to delete this Branch?",
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
                var response = await Http.DeleteAsync($"Master/DeleteBranch/{branch.BranchId}");
                if (response.IsSuccessStatusCode)
                {
                    branches.Remove(branch);
                    await grid.Reload();
                    Notifier.Success("Deleted Successfully", "Branch has been deleted successfully.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Notifier.Error("Delete Failed", errorContent);
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Delete Failed", $"An error occurred while deleting the branch: {ex.Message}");
            }

            IsDelLoading = null;
        }

        private async Task ToggleLockAsync(BranchesDto item, bool newValue)
        {
            try
            {
                // Option 1: Call the dedicated ToggleLock API
                var response = await Http.PutAsync($"Master/ToggleLock/{item.BranchId}", null);

                if (response.IsSuccessStatusCode)
                {
                    item.LockCapital = newValue;
                    Notifier.Success("Lock Updated Successfully", $"Lock updated for branch {item.BranchName}.");
                }
                else
                {
                    // Revert toggle if API fails
                    item.LockCapital = !newValue;
                    var msg = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Failed to update lock: {msg}");
                    Notifier.Error("Error updating lock");
                }
            }
            catch (Exception ex)
            {
                item.LockCapital = !newValue; // revert
                Console.WriteLine($"Error updating lock: {ex.Message}");
                Notifier.Error("Error updating lock");
            }
        }

    }
}
