using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen;
using Radzen.Blazor;
using System.Text.Json;

namespace ppfc.web.Pages.Master
{
    public partial class Interest
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;
        [Inject] UserContext UserContext { get; set; }

        private RadzenDataGrid<InterestDto> grid;
        public List<InterestDto> interests = new();

        public bool IsLoading = true;
        public int companyId { get; set; }

        protected override async Task OnInitializedAsync()
        {
            companyId = UserContext.CompanyId;

            await LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                interests = await Http.GetFromJsonAsync<List<InterestDto>>($"Master/GetInterest/{companyId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load Interests: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task EditInterest(InterestDto interest)
        {
            await grid.EditRow(interest);
            await Task.Yield();
        }

        public async Task CancelEdit(InterestDto interest)
        {
            grid.CancelEditRow(interest);
            StateHasChanged();
        }

        public async Task SaveInterest(InterestDto interest)
        {
            if (string.IsNullOrWhiteSpace(interest.Type))
            {
                Notifier.Warning("Interest Type cannot be empty.");
                return;
            }
            if (interest.Interest == null || interest.Interest <= 0)
            {
                Notifier.Warning("Interest rate must be greater than zero.");
                return;
            }

            try
            {
                var response = await Http.PutAsJsonAsync("Master/UpdateInterest", interest);
                if (response.IsSuccessStatusCode)
                {
                    await LoadData();
                    Notifier.Success("Interest saved successfully.");
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
                Notifier.Error("Exception occurred while saving Interest.", ex.Message);
            }
        }

        public async Task DeleteInterest(InterestDto interest)
        {
            bool? confirmed = await DialogService.Confirm(
                            $"Are you sure you want to delete this Interest?",
                            "Confirm Delete",
                            new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
                        );

            if (confirmed != true)
            {
                return;
            }

            try
            {
                var response = await Http.DeleteAsync($"Master/DeleteInterest/{interest.InterestId}");
                if (response.IsSuccessStatusCode)
                {
                    interests.Remove(interest);
                    await grid.Reload();
                    Notifier.Success("Interest deleted successfully.");
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Failed to Delete!", message);
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Exception occurred while deleting Interest.", ex.Message);
            }
        }
    }
}
