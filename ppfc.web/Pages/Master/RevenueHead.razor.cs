using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen;
using Radzen.Blazor;
using System.Text.Json;
using static System.Net.WebRequestMethods;

namespace ppfc.web.Pages.Master
{
    public partial class RevenueHead
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;
        [Inject] UserContext UserContext { get; set; }

        private RadzenDataGrid<RevenueHeadDto> grid;
        public List<BranchesDto> branches = new();
        public List<RevenueHeadDto> revenueHeads = new();

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
                revenueHeads = await Http.GetFromJsonAsync<List<RevenueHeadDto>>($"Master/GetRevenueHeads/{companyId}");
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

        public async Task EditRevenueHead(RevenueHeadDto revenueHead)
        {
            await grid.EditRow(revenueHead);
            await Task.Yield();
        }

        public async Task CancelEdit(RevenueHeadDto revenueHead)
        {
            grid.CancelEditRow(revenueHead);
            StateHasChanged();
        }

        public async Task AddNew() {             
            var newRevenueHead = new RevenueHeadDto
            {
                RevenueHeadId = 0,
                RevenueHeadName = string.Empty,
                BranchId = 0,
                CompanyId = companyId,
                IsNew = true
            };
            revenueHeads.Insert(0, newRevenueHead);
            await grid.InsertRow(newRevenueHead);
        }

        public async Task SaveRevenueHead(RevenueHeadDto revenueHead)
        {
            if(string.IsNullOrWhiteSpace(revenueHead.RevenueHeadName))
            {
                Notifier.Warning("Revenue Head Name cannot be empty.");
                return;
            }
            if (revenueHead.BranchId == 0)
            {
                Notifier.Warning("Please select a Branch.");
                return;
            }

            try
            {
                if(revenueHead.RevenueHeadId == 0)
                {
                    var response = await Http.PostAsJsonAsync("Master/AddRevenueHead", revenueHead);
                    if (response.IsSuccessStatusCode)
                    {
                        await LoadData();
                        Notifier.Success("Revenue Head created successfully.");
                    }
                    else
                    {
                        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                        string message = json.GetProperty("message").GetString();
                        Notifier.Error("Failed to create Revenue Head!", message);
                    }
                }
                else
                {
                    var response = await Http.PutAsJsonAsync("Master/UpdateRevenueHead", revenueHead);
                    if (response.IsSuccessStatusCode)
                    {
                        await grid.UpdateRow(revenueHead);
                        Notifier.Success("Revenue Head updated successfully.");
                    }
                    else
                    {
                        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                        string message = json.GetProperty("message").GetString();
                        Notifier.Error("Failed to update Revenue Head!", message);
                    }
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", ex.Message);
            }
        }

        public async Task DeleteRevenueHead(RevenueHeadDto revenueHead)
        {
            bool? confirmed = await DialogService.Confirm(
                $"Are you sure you want to delete this Revenue Head?",
                "Confirm Delete",
                new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
            );

            if (confirmed != true)
            {
                return;
            }

            try
            {
                var response = await Http.DeleteAsync($"Master/DeleteRevenueHead/{revenueHead.RevenueHeadId}");
                if (response.IsSuccessStatusCode)
                {
                    revenueHeads.Remove(revenueHead);
                    await grid.Reload();
                    Notifier.Success("Revenue Head deleted successfully.");
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Failed to delete Revenue Head.",message);
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", ex.Message);
            }
        }

    }
}
