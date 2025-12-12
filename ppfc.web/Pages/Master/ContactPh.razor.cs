using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen;
using Radzen.Blazor;
using System.ComponentModel.Design;
using System.Numerics;

namespace ppfc.web.Pages.Master
{
    public partial class ContactPh
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;

        private RadzenDataGrid<ContactPhoneDto> grid;
        public List<ContactPhoneDto> contacts = new();

        public bool IsLoading = true;

        protected override async Task OnInitializedAsync()
        {
            await LoadData();
        }

        public async Task LoadData()
        {
            IsLoading = true;
            try
            {
                contacts = await Http.GetFromJsonAsync<List<ContactPhoneDto>>($"Master/GetContactPhoneNumbers");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load Contact Phones: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task EditContact(ContactPhoneDto contact)
        {
            await grid.EditRow(contact);
            await Task.Yield();
        }

        public async Task CancelEdit(ContactPhoneDto contact)
        {
            grid.CancelEditRow(contact);
            StateHasChanged();
        }

        public async Task SaveContact(ContactPhoneDto contact)
        {
            if (string.IsNullOrWhiteSpace(contact.PhoneNumber))
            {
                Notifier.Warning("Enter Contact Phone Number");
                return;
            }

            try
            {
                var response = await Http.PutAsJsonAsync("Master/UpdateContactPhoneNumber", contact);
                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Contact Phone Number saved successfully.");
                    await grid.UpdateRow(contact);
                }
                else
                {
                    Notifier.Error("Failed to save Contact Phone Number.");
                }
            }
            catch (Exception ex)
            {
                Notifier.Error($"Error: {ex.Message}");
            }
        }

        public async Task DeleteContact(ContactPhoneDto contact)
        {
            bool? confirmed = await DialogService.Confirm(
                $"Are you sure you want to delete this Contact Phone Number?",
                "Confirm Delete",
                new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
            );

            if (confirmed != true)
            {
                return;
            }

            try
            {
                var response = await Http.DeleteAsync($"Master/DeleteContactPhoneNumbers");
                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Contact Phone Number deleted successfully.");
                    contacts.Remove(contact);
                    await grid.Reload();
                }
                else
                {
                    Notifier.Error("Failed to delete Contact Phone Number.");
                }
            }
            catch (Exception ex)
            {
                Notifier.Error($"Error: {ex.Message}");
            }
        }
    }
}
