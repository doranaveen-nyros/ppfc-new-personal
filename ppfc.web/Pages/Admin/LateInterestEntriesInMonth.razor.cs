using Microsoft.AspNetCore.Components;
using ppfc.web.Helpers;

namespace ppfc.web.Pages.Admin
{
    public partial class LateInterestEntriesInMonth
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;

        private int lateInterestEntriesInMonth { get; set; }

        private bool IsLoading = true;
        private bool IsUpdating = false;

        protected override async Task OnInitializedAsync()
        {
            await LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                lateInterestEntriesInMonth = await Http.GetFromJsonAsync<int>("Admin/GetCampChargeInMonth");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load Late Interest Entries In Month: {ex.Message}");
                Notifier.Error("Error", "Failed to load Late Interest Entries In Month.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task UpdateLateInterest()
        {
            IsUpdating = true;
            try
            {
                var response = await Http.PutAsJsonAsync("Admin/UpdateCampChargeInMonth", lateInterestEntriesInMonth);
                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Success", "Late Interest Entries In Month updated successfully.");
                }
                else
                {
                    Notifier.Error("Error", "Failed to update Late Interest Entries In Month.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to update Late Interest Entries In Month: {ex.Message}");
                Notifier.Error("Error", "Failed to update Late Interest Entries In Month.");
            }
            finally
            {
                IsUpdating = false;
            }
        }
    }
}
