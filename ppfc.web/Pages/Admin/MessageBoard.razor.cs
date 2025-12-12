using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen;
using Radzen.Blazor;
using System.ComponentModel.Design;
using static System.Net.WebRequestMethods;

namespace ppfc.web.Pages.Admin
{
    public partial class MessageBoard
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;

        private RadzenDataGrid<MessageDTO> grid;
        private List<MessageDTO> messages = new();

        private bool IsLoading = true;
        public bool IsSubmitting = false;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                // Specify the type argument explicitly to fix CS0411
                messages = await Http.GetFromJsonAsync<List<MessageDTO>>("Admin/GetMessages");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load messages: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task AddNew()
        {
            var newMsg = new MessageDTO
            {
                MessageId = 0,
                Message = "",
                DateTime = DateTime.Now
            };

            messages.Insert(0, newMsg);
            await grid.InsertRow(newMsg);
        }

        private async Task EditRow(MessageDTO msg)
        {
            await grid!.EditRow(msg);
        }

        private async Task SaveMessage(MessageDTO msg)
        {
            IsSubmitting = true;

            if (msg == null)
            {
                Notifier.Warning("Invalid Data", "Message data is invalid.");
                IsSubmitting = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(msg.Message))
            {
                Notifier.Warning("Validation", "Please enter a message before submitting.");
                IsSubmitting = false;
                return;
            }

            try
            {
                HttpResponseMessage response;

                if(msg.MessageId == 0)
                {
                    response = await Http.PostAsJsonAsync("Admin/AddMessage", msg);
                }
                else
                {
                    response = await Http.PutAsJsonAsync($"Admin/UpdateMessage/{msg.MessageId}", msg);
                }

                if (response.IsSuccessStatusCode)
                {
                    if (msg.MessageId == 0)
                    {
                        Notifier.Success("Added Successfully", "New message added successfully.");
                    }
                    else
                    {
                        Notifier.Success("Updated Successfully", "Message updated successfully.");
                    }

                    messages = await Http.GetFromJsonAsync<List<MessageDTO>>($"Admin/GetMessages");
                    await grid.Reload();
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    Notifier.Error("Update Failed", $"Failed to update user. {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", $"An error occurred: {ex.Message}");
            }

            IsSubmitting = false;
        }

        private async Task CancelEdit(MessageDTO msg)
        {
            messages.Remove(msg);
            await grid.Reload();
        }

        private async Task DeleteMessage(MessageDTO msg)
        {
            if (msg == null || msg.MessageId == 0)
            {
                Notifier.Warning("Invalid Selection", "Please select a valid message to delete.");
                return;
            }

            bool? confirm = await DialogService.Confirm(
                $"Are you sure you want to delete '{msg.Message}'?",
                "Confirm Delete",
                new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
            );

            if (confirm == true)
            {
                try
                {
                    var response = await Http.DeleteAsync($"Admin/DeleteMessage/{msg.MessageId}");

                    if (response.IsSuccessStatusCode)
                    {
                        messages.Remove(msg);
                        await grid.Reload();
                        Notifier.Success("Deleted", "Message deleted successfully.");
                    }
                    else
                    {
                        var errorMsg = await response.Content.ReadAsStringAsync();
                        Notifier.Error("Error", errorMsg);
                    }
                }
                catch (Exception ex)
                {
                    Notifier.Error("Error", $"Error deleting message: {ex.Message}");
                }
            }
        }

    }
}
