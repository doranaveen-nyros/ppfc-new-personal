using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using Radzen;
using Radzen.Blazor;
using System.Text.Json;

namespace ppfc.web.Pages.Master
{
    public partial class NoticeType
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;
        
        private RadzenDataGrid<NoticeTypeDto> grid;
        public List<NoticeTypeDto> notices = new();
        NoticeTypeDto modalModel = new NoticeTypeDto();

        public bool IsLoading = true;
        public bool isModalOpen = false;
        public bool isEditing = false;

        protected override async Task OnInitializedAsync()
        {
            await LoadData();
        }

        private async Task LoadData()
        {
            IsLoading = true;
            try
            {
                notices = await Http.GetFromJsonAsync<List<NoticeTypeDto>>($"Master/GetNoticeTypes");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load Notice Types: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (text.Length <= maxLength) return text;
            return text.Substring(0, maxLength).TrimEnd() + " ...";
        }

        // Open modal for add or edit
        public void OpenModal(bool isEdit, NoticeTypeDto? model = null)
        {
            isEditing = isEdit;
            if (isEdit && model != null)
            {
                // deep copy to modalModel to avoid mutating grid until save
                modalModel = new NoticeTypeDto
                {
                    NoticeTypeId = model.NoticeTypeId,
                    NoticeType = model.NoticeType,
                    Notice = model.Notice,
                    IsNew = model.IsNew
                };
            }
            else
            {
                modalModel = new NoticeTypeDto { IsNew = true };
            }
            isModalOpen = true;
        }

        public void CloseModal()
        {
            isModalOpen = false;
        }

        // Edit action (opens modal populated)
        public void EditRow(NoticeTypeDto item)
        {
            OpenModal(isEdit: true, model: item);
        }

        // Modal submit (Add or Edit)
        public async Task OnModalSubmit()
        {
            if (string.IsNullOrWhiteSpace(modalModel.NoticeType))
            {
                Notifier.Warning("Validation", "Notice Type is required");
                return;
            }
            if (string.IsNullOrWhiteSpace(modalModel.Notice))
            {
                Notifier.Warning("Validation", "Notice text is required");
                return;
            }

            if (isEditing)
            {
                // update existing
                var response = await Http.PutAsJsonAsync("Master/UpdateNoticeType", modalModel);
                if (response.IsSuccessStatusCode)
                {
                    await LoadData();
                    Notifier.Success("Notice updated successfully.");
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Failed to update Notice!", message);
                }
            }
            else
            {
                // add new
                var response = await Http.PostAsJsonAsync("Master/AddNoticeType", modalModel);
                if (response.IsSuccessStatusCode)
                {
                    await LoadData();
                    Notifier.Success("Notice created successfully.");
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Failed to create Notice!", message);
                }                
            }

            // close modal
            isModalOpen = false;
        }

        public async Task ConfirmDelete(NoticeTypeDto notice)
        {
            bool? confirmed = await DialogService.Confirm(
                $"Are you sure you want to delete this Notice?",
                "Confirm Delete",
                new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
            );

            if (confirmed != true)
            {
                return;
            }

            try
            {
                var response = await Http.DeleteAsync($"Master/DeleteNoticeType/{notice.NoticeTypeId}");
                if (response.IsSuccessStatusCode)
                {
                    notices.Remove(notice);
                    await grid.Reload();
                    Notifier.Success("Notice deleted successfully.");
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Failed to delete Area!", message);
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", ex.Message);
            }
        }
    }
}
