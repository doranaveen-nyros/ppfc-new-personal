using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen;
using Radzen.Blazor;
using System.Text.Json;

namespace ppfc.web.Pages.Admin
{
    public partial class HpChargeDuration
    {
        [Inject] UserContext UserContext { get; set; }
        [Inject] protected AppNotifier Notifier { get; set; } = default!;       

        private RadzenDataGrid<HPChargeDurationDto> grid;
        public List<HPChargeDurationDto> hpChargeDurations = new();
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
                hpChargeDurations = await Http.GetFromJsonAsync<List<HPChargeDurationDto>>($"Admin/GetHpChargeDuration/{companyId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load HP Charge Durations: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task AddNew()
        {
            var newDuration = new HPChargeDurationDto
            {
                HPChargeDurationId = 0,
                FromDate = DateTime.Now.AddMonths(-1),
                ToDate = DateTime.Now,
                HPChargeInstallments = 0
            };
            hpChargeDurations.Insert(0, newDuration);
            await grid.InsertRow(newDuration);
        }

        public async Task EditDuration(HPChargeDurationDto duration)
        {
            await grid.EditRow(duration);
            await Task.Yield();
        }

        public async Task CancelEdit(HPChargeDurationDto duration)
        {
            grid.CancelEditRow(duration);
            StateHasChanged();
        }

        public async Task SaveDuration(HPChargeDurationDto duration)
        {
            try
            {
                // Validate dates
                if (!duration.FromDate.HasValue || !duration.ToDate.HasValue)
                {
                    Notifier.Error("Please enter valid dates.");
                    return;
                }

                if (!duration.HPChargeInstallments.HasValue || duration.HPChargeInstallments.Value <= 0)
                {
                    Notifier.Error("Please enter HP Charge Inst's.");
                    return;
                }

                // Create request object
                var request = new
                {
                    FromDateDay = duration.FromDate.Value.Day,
                    FromDateMonth = duration.FromDate.Value.Month,
                    FromDateYear = duration.FromDate.Value.Year,
                    ToDateDay = duration.ToDate.Value.Day,
                    ToDateMonth = duration.ToDate.Value.Month,
                    ToDateYear = duration.ToDate.Value.Year,
                    HPChargeInstallments = duration.HPChargeInstallments.Value
                };

                HttpResponseMessage response;

                if (duration.HPChargeDurationId == 0)
                {
                    // Insert new record
                    response = await Http.PostAsJsonAsync($"Admin/CreateHPChargeDuration/{companyId}", request);
                }
                else
                {
                    // Update existing record
                    response = await Http.PutAsJsonAsync($"Admin/UpdateHPChargeDuration/{duration.HPChargeDurationId}?companyId={companyId}", request);
                }

                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("HP Charge Duration saved successfully.");
                    await LoadData();
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Failed!", message);
                }
            }
            catch (Exception ex)
            {
                Notifier.Error($"Error saving HP Charge Duration: {ex.Message}");
            }
        }

        public async Task DeleteDuration(HPChargeDurationDto duration)
        {
            bool? confirmed = await DialogService.Confirm(
                $"Are you sure you want to delete this HP Charge Duration?",
                "Confirm Delete",
                new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
            );
            if (confirmed == true)
            {
                try
                {
                    var response = await Http.DeleteAsync($"Admin/DeleteHpChargeDuration/{duration.HPChargeDurationId}");
                    if (response.IsSuccessStatusCode)
                    {
                        hpChargeDurations.Remove(duration);
                        await grid.Reload();
                        Notifier.Success("HP Charge Duration deleted successfully.");
                    }
                    else
                    {
                        Notifier.Error("Failed to delete HP Charge Duration.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error deleting HP Charge Duration: {ex.Message}");
                    Notifier.Error("An error occurred while deleting.");
                }
            }
        }
    }
}
