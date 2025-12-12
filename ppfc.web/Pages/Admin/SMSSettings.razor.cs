using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen.Blazor;

namespace ppfc.web.Pages.Admin
{
    public partial class SMSSettings
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;

        private RadzenDataGrid<BusinessSMSDto> grid;
        public List<BusinessSMSDto> smsSettings = new();

        public bool IsLoading = true;

        protected override async Task OnInitializedAsync()
        {
            await LoadData();
        }

        private async Task LoadData()
        {
            IsLoading = true;
            try
            {
                smsSettings = await Http.GetFromJsonAsync<List<BusinessSMSDto>>("Admin/GetBusinessSMS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load SMS settings: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task EditSettings(BusinessSMSDto setting)
        {
            await grid.EditRow(setting);
            await Task.Yield();
        }

        public async Task CancelEdit(BusinessSMSDto setting)
        {
            grid.CancelEditRow(setting);

            if (string.IsNullOrEmpty(setting.UserId) && string.IsNullOrEmpty(setting.Password))
            {
                smsSettings.Remove(setting);
                await grid.Reload();
            }

            StateHasChanged();
        }

        public async Task SaveSettings(BusinessSMSDto setting)
        {
            try
            {
                var response = await Http.PostAsJsonAsync("Admin/UpdateBusinessSMSSettings", setting);
                if (response.IsSuccessStatusCode)
                {
                    await grid.UpdateRow(setting);
                    Notifier.Success("Updated Successfully", "SMS settings have been updated successfully.");
                }
                else
                {
                    Notifier.Error("Error", $"Failed to save SMS settings: {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", $"An error occurred while saving SMS settings: {ex.Message}");
            }
        }
    }
}
