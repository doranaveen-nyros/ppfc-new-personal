using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen;
using Radzen.Blazor;
using System.Text.Json;

namespace ppfc.web.Pages.Admin
{
    public partial class ReceiptLatePayment
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;

        private List<ReceiptLatePaymentDto> receiptLatePayments = new();
        private RadzenDataGrid<ReceiptLatePaymentDto> grid;

        private bool IsLoading = true;

        protected override async Task OnInitializedAsync()
        {
            await LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                receiptLatePayments = await Http.GetFromJsonAsync<List<ReceiptLatePaymentDto>>("Admin/GetReceiptLatePayment");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load Receipt Late Payments: {ex.Message}");
                Notifier.Error("Error", "Failed to load Receipt Late Payments.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task AddNew()
        {
            var newReceiptLatePayment = new ReceiptLatePaymentDto
            {
                RLPId = 0,
                Installment = 0,
                Percentage = 0,
                IsNew = true
            };
            receiptLatePayments.Insert(0, newReceiptLatePayment);
            await grid.InsertRow(newReceiptLatePayment);
        }

        public async Task EditRow(ReceiptLatePaymentDto latePayment)
        {
            await grid.EditRow(latePayment);
            await Task.Yield();
        }
        public async Task CancelEdit(ReceiptLatePaymentDto latePayment)
        {
            grid.CancelEditRow(latePayment);
            StateHasChanged();
        }

        public async Task SaveRow(ReceiptLatePaymentDto latePayment)
        {
            if(latePayment.Installment <= 0)
            {
                Notifier.Warning("Validation Error", "Installment must be a valid number.");
                return;
            }

            if (latePayment.RLPId == 0)
            {
                // New entry
                var response = await Http.PostAsJsonAsync("Admin/InsertReceiptLatePayment", latePayment);
                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Success", "Receipt Late Payment created successfully.");
                }
                else
                {
                    //Notifier.Error("Error", "Failed to create Receipt Late Payment.");
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Insert Failed", message);
                }
            }
            else
            {
                // Existing entry
                var response = await Http.PutAsJsonAsync($"Admin/UpdateReceiptLatePayment/{latePayment.RLPId}", latePayment);
                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Success", "Receipt Late Payment updated successfully.");                    
                }
                else
                {
                    Notifier.Error("Error", "Failed to update Receipt Late Payment.");
                }
            }
            await LoadData();
        }

        public async Task DeleteRow(ReceiptLatePaymentDto latePayment)
        {
            bool? confirmed = await DialogService.Confirm(
                $"Are you sure you want to delete this Receipt Late Payment?",
                "Confirm Delete",
                new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
            );

            if (confirmed != true)
            {
                return;
            }
            try
            {
                var response = await Http.DeleteAsync($"Admin/DeleteReceiptLatePayment/{latePayment.RLPId}");
                if (response.IsSuccessStatusCode)
                {
                    receiptLatePayments.Remove(latePayment);
                    await grid.Reload();
                    Notifier.Success("Success", "Receipt Late Payment deleted successfully.");
                }
                else
                {
                    Notifier.Error("Error", "Failed to delete Receipt Late Payment.");
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", $"An error occurred: {ex.Message}");
                return;

            }
        }
    }
}
