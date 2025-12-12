using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen;
using Radzen.Blazor;
using System.Text.Json;
using static System.Net.WebRequestMethods;

namespace ppfc.web.Pages.Master
{
    public partial class RTOAgent
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;
        [Inject] UserContext UserContext { get; set; }
        [Inject] IJSRuntime JS { get; set; }

        private RadzenDataGrid<RTOAgentDto> grid;
        public List<BranchesDto> branches = new();
        public List<RTOAgentDto> rtoAgents = new();

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
                rtoAgents = await Http.GetFromJsonAsync<List<RTOAgentDto>>($"Master/GetRTOAgents/{companyId}");
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

        public async Task EditRTOAgent(RTOAgentDto agent)
        {
            await grid.EditRow(agent);
            await Task.Yield();
        }

        public async Task CancelEdit(RTOAgentDto agent)
        {
            grid.CancelEditRow(agent);
            StateHasChanged();
        }

        public async Task AddNew()
        {
            var newAgent = new RTOAgentDto
            {
                CompanyId = companyId,
                IsNew = true
            };

            rtoAgents.Insert(0, newAgent);
            await grid.InsertRow(newAgent);
        }

        private bool ValidateRTOAgent(RTOAgentDto agent)
        {
            if (string.IsNullOrWhiteSpace(agent.RTOAgentName))
            {
                Notifier.Warning("Validation Error", "RTO Agent Name is required.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(agent.PhoneNo))
            {
                Notifier.Warning("Validation Error", "RTO Agent Phone Number is required.");
                return false;
            }
            if (agent.BranchId == 0)
            {
                Notifier.Warning("Validation Error", "Please select a Branch.");
                return false;
            }
            return true;
        }

        public async Task SaveRTOAgent(RTOAgentDto agent)
        {
            if (!ValidateRTOAgent(agent))
            {
                return;
            }

            try
            {
                if (agent.RTOAgentId == 0)
                {
                    // Call API to add new RTO Agent
                    var response = await Http.PostAsJsonAsync("Master/AddRTOAgent", agent);
                    if (response.IsSuccessStatusCode)
                    {
                        await LoadData();
                        Notifier.Success("RTO Agent added successfully.");
                    }
                    else
                    {
                        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                        string message = json.GetProperty("message").GetString();
                        Notifier.Error("Failed to add RTO Agent!", message);
                    }
                }
                else
                {
                    // Call API to update existing RTO Agent
                    var response = await Http.PutAsJsonAsync("Master/UpdateRTOAgent", agent);
                    if (response.IsSuccessStatusCode)
                    {
                        await grid.UpdateRow(agent);
                        Notifier.Success("RTO Agent updated successfully.");
                    }
                    else
                    {
                        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                        string message = json.GetProperty("message").GetString();
                        Notifier.Error("Failed to update RTO Agent!", message);
                    }
                }
            }
            catch (Exception ex)
            {
                Notifier.Error($"Error: {ex.Message}");
            }
        }

        public async Task DeleteRTOAgent(RTOAgentDto agent)
        {
            bool? confirmed = await DialogService.Confirm(
                $"Are you sure you want to delete this RTO Agent?",
                "Confirm Delete",
                new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
            );

            if (confirmed != true)
            {
                return;
            }

            try
            {
                var response = await Http.DeleteAsync($"Master/DeleteRTOAgent/{agent.RTOAgentId}");
                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("RTO Agent deleted successfully.");
                    rtoAgents.Remove(agent);
                    await grid.Reload();
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Failed to delete RTO Agent!", message);
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", ex.Message);
                return;
            }
        }

        private async Task ToggleLockAsync(RTOAgentDto item, bool newValue)
        {
            try
            {
                // Option 1: Call the dedicated ToggleLock API
                var response = await Http.PutAsync($"Master/ToggleRTOAgentLock/{item.RTOAgentId}", null);

                if (response.IsSuccessStatusCode)
                {
                    item.LockAgent = newValue;
                    Notifier.Success("Lock Updated Successfully", $"Lock updated for branch {item.RTOAgentName}.");
                }
                else
                {
                    // Revert toggle if API fails
                    item.LockAgent = !newValue;
                    var msg = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Failed to update lock: {msg}");
                    Notifier.Error("Error updating lock");
                }
            }
            catch (Exception ex)
            {
                item.LockAgent = !newValue; // revert
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
            csv.AppendLine("RTO Agent Name,PhoneNo,Branch,Lock Status");

            foreach (var a in grid.PagedView) // ✅ only visible rows
            {
                var cleanName = a.RTOAgentName?.Replace("\"", "\"\"");
                var branchName = branches.FirstOrDefault(b => b.BranchId == a.BranchId)?.BranchName ?? "";
                var lockStatus = a.LockAgent ? "Locked" : "Unlocked";

                csv.AppendLine($"\"{cleanName}\",{a.PhoneNo},\"{branchName}\",{lockStatus}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"RTOAgents_{DateTime.Now:dd-MMM-yyyy_hh-mm-tt}.csv";

            await JS.InvokeVoidAsync("downloadFile", fileName, "text/csv", Convert.ToBase64String(bytes));
        }

    }
}
