using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen.Blazor;

namespace ppfc.web.Pages.Master
{
    public partial class BirthdayMessages
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;

        private RadzenDataGrid<BirthMessageDto> grid;
        public List<BirthMessageDto> messages = new();

        public bool IsLoading = true;
        public int companyId { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await LoadData();
        }

        private async Task LoadData()
        {
            IsLoading = true;
            try
            {
                messages = await Http.GetFromJsonAsync<List<BirthMessageDto>>($"Master/GetBirthMessages");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load Birthday Messages: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task EditPDRLockLimit(BirthMessageDto msg)
        {
            await grid.EditRow(msg);
            await Task.Yield();
        }

        public async Task CancelEdit(BirthMessageDto msg)
        {
            grid.CancelEditRow(msg);
            await Task.Yield();
        }

        public async Task SaveBirthdayMessage(BirthMessageDto msg)
        {
            try
            {
                var response = await Http.PutAsJsonAsync($"Master/UpdateBirthMessage", msg);
                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Birthday Message saved successfully.");
                    await LoadData();
                }
                else
                {
                    Notifier.Error("Failed to save Birthday Message.");
                }
            }
            catch (Exception ex)
            {
                Notifier.Error($"Error: {ex.Message}");
            }
        }
    }
}
