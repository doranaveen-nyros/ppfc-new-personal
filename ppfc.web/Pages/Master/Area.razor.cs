using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen;
using Radzen.Blazor;
using System.Text.Json;

namespace ppfc.web.Pages.Master
{
    public partial class Area
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;
        [Inject] UserContext UserContext { get; set; }

        private RadzenDataGrid<AreasDto> grid;
        public List<BranchesDto> branches = new();
        public List<AreasDto> areas = new();

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
                areas = await Http.GetFromJsonAsync<List<AreasDto>>($"Master/GetAreas/{companyId}");
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

        public async Task EditArea(AreasDto area)
        {
            await grid.EditRow(area);
            await Task.Yield();
        }

        public async Task CancelEdit(AreasDto area)
        {
            grid.CancelEditRow(area);
            StateHasChanged();
        }

        public async Task AddNew()
        {
            var newArea = new AreasDto
            {
                AreaId = 0,
                AreaName = string.Empty,
                AreaCode = string.Empty,
                BranchId = 0,
                Lock = false,
                IsNew = true
            };

            areas.Insert(0, newArea);
            await grid.InsertRow(newArea);
        }

        public async Task SaveArea(AreasDto area)
        {
            if(area.BranchId == 0)
            {
                Notifier.Warning("Please select a Branch.");
                return;
            }
            if (string.IsNullOrWhiteSpace(area.AreaName))
            {
                Notifier.Warning("Validation Error", "Enter Area Name.");
                return;
            }
            //if (string.IsNullOrWhiteSpace(area.AreaCode))
            //{
            //    Notifier.Warning("Validation Error", "Enter Area Code.");
            //    return;
            //}

            try
            {
                if (area.AreaId == 0)
                {
                    var response = await Http.PostAsJsonAsync("Master/AddArea", area);
                    if (response.IsSuccessStatusCode)
                    {
                        await LoadData();
                        Notifier.Success("Area created successfully.");
                    }
                    else
                    {
                        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                        string message = json.GetProperty("message").GetString();
                        Notifier.Error("Failed to create Area!", message);
                    }
                }
                else
                {
                    var response = await Http.PutAsJsonAsync("Master/UpdateArea", area);
                    if (response.IsSuccessStatusCode)
                    {
                        await grid.UpdateRow(area);
                        Notifier.Success("Area updated successfully.");
                    }
                    else
                    {
                        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                        string message = json.GetProperty("message").GetString();
                        Notifier.Error("Failed to update Area!", message);
                    }
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", ex.Message);
            }
        }

        public async Task DeleteArea(AreasDto area)
        {
            bool? confirmed = await DialogService.Confirm(
                $"Are you sure you want to delete this Area?",
                "Confirm Delete",
                new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
            );

            if (confirmed != true)
            {
                return;
            }

            try
            {
                var response = await Http.DeleteAsync($"Master/DeleteArea/{area.AreaId}");
                if (response.IsSuccessStatusCode)
                {
                    areas.Remove(area);
                    await grid.Reload();
                    Notifier.Success("Area deleted successfully.");
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Failed to delete Area!", message);
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", ex.Message);
            }
        }

        private async Task ToggleLockAsync(AreasDto item, bool newValue)
        {
            try
            {
                // Option 1: Call the dedicated ToggleLock API
                var response = await Http.PutAsync($"Master/ToggleAreaLock/{item.AreaId}", null);

                if (response.IsSuccessStatusCode)
                {
                    item.Lock = newValue;
                    Notifier.Success("Lock Updated Successfully", $"Lock updated for branch {item.AreaName}.");
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
    }
}
