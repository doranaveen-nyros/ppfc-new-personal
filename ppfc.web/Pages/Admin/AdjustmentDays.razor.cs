using Microsoft.AspNetCore.Components;
using ppfc.web.Helpers;
using static System.Net.WebRequestMethods;

namespace ppfc.web.Pages.Admin
{
    public partial class AdjustmentDays
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;

        private int adjustmentDays;
        private int oldDataLimit;
        private bool IsLoading = true;
        public bool IsUpdating = false;
        public bool IsChanging = false;

        private List<int> dayOptions = Enumerable.Range(1, 60).Select(x => x * 5).ToList();

        protected override async Task OnInitializedAsync()
        {
            await LoadData();
        }

        private async Task LoadData()
        {
            adjustmentDays = await Http.GetFromJsonAsync<int>("Admin/GetAdjustmentDays");
            oldDataLimit = await Http.GetFromJsonAsync<int>("Admin/GetOldDataLimit");

            IsLoading = false;
        }

        private async Task UpdateAdjustmentDays()
        {
            IsUpdating = true;

            if (adjustmentDays < 0)
            {
                Notifier.Warning("Validation", "Please enter a valid number of Adjustment Days.");
                IsUpdating = false;
                return;
            }

            try
            {
                var response = await Http.PostAsJsonAsync("Admin/UpdateAdjustmentDays", adjustmentDays);

                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Updated Successfully", "Adjustment Days updated successfully.");
                    await LoadData();
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    Notifier.Error("Update Failed", $"Failed to update Adjustment Days. {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", $"An error occurred: {ex.Message}");
            }

            IsUpdating = false;
        }

        private async Task UpdateOldDataLimit()
        {
            IsChanging = true;

            if (oldDataLimit <= 0)
            {
                Notifier.Warning("Validation", "Please select a valid Old Data Limit.");
                IsChanging = false;
                return;
            }

            try
            {
                var response = await Http.PostAsJsonAsync("Admin/UpdateOldDataLimit", oldDataLimit);

                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Updated Successfully", "Old Data Limit updated successfully.");
                    await LoadData();
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    Notifier.Error("Update Failed", $"Failed to update Old Data Limit. {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", $"An error occurred: {ex.Message}");
            }

            IsChanging = false;
        }
    }
}
