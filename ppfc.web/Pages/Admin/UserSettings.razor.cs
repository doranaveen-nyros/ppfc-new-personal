using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using System.Data;
using static System.Net.WebRequestMethods;

namespace ppfc.web.Pages.Admin
{
    public partial class UserSettings
    {
        [Inject] UserContext UserContext { get; set; }
        [Inject] protected AppNotifier Notifier { get; set; }

        public List<UserDto> users = new();
        private List<UserSettingsDto> userSettings = new();
        private List<UserSettingsDto> originalUserSettings = new();
        private Dictionary<string, List<UserSettingsDto>> categorizedSettings = new();

        public bool IsLoading = true;
        public bool IsSubmitting = false;

        protected int? selectedUserId;
        protected string? selectedModule;
        private string activeModule = "Admin";
        public int companyId { get; set; }

        // Color/module logic
        private readonly Dictionary<string, (string color, string module)> colorTriggers = new()
        {
            ["Adjustment Days"] = ("chocolate", "Admin"),
            ["Area"] = ("darkblue", "Master"),
            ["AC Close"] = ("red", "Transaction"),
            ["ATC Audit"] = ("darkgreen", "Audit"),
            ["ACClose HPReturn Report"] = ("black", "Reports")
        };

        protected override async Task OnInitializedAsync()
        {
            companyId = UserContext.CompanyId;

            try
            {
                users = await Http.GetFromJsonAsync<List<UserDto>>($"Admin/GetUsersForSettings/{companyId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load roles: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        //private async Task OnUserChanged(ChangeEventArgs e)
        //{
        //    if (int.TryParse(e.Value?.ToString(), out int userId))
        //    {
        //        selectedUserId = userId;

        //        userSettings = await Http.GetFromJsonAsync<List<UserSettingsDto>>($"Admin/GetUserSettings/{userId}")
        //            ?? new List<UserSettingsDto>();

        //        originalUserSettings = userSettings
        //                .Select(u => CloneUserSettingsDto(u))
        //                .ToList();

        //        CategorizeUserSettings();
        //        ApplyRestrictions();
        //    }
        //}

        private async Task OnUserChanged(object value)
        {
            if (value is int userId)
            {
                selectedUserId = userId;

                userSettings = await Http.GetFromJsonAsync<List<UserSettingsDto>>($"Admin/GetUserSettings/{userId}")
                    ?? new List<UserSettingsDto>();

                originalUserSettings = userSettings
                    .Select(u => CloneUserSettingsDto(u))
                    .ToList();

                CategorizeUserSettings();
                ApplyRestrictions();
            }
        }


        private void SelectTab(string module)
        {
            activeModule = module;
        }

        private void CategorizeUserSettings()
        {
            categorizedSettings.Clear();
            string currentModule = "Master";
            string currentColor = "darkblue";

            foreach (var s in userSettings)
            {
                if (colorTriggers.TryGetValue(s.ScreenName.Trim(), out var category))
                {
                    currentModule = category.module;
                    currentColor = category.color;
                }

                if (!categorizedSettings.ContainsKey(currentModule))
                    categorizedSettings[currentModule] = new List<UserSettingsDto>();

                categorizedSettings[currentModule].Add(s);
            }
        }

        private void ApplyRestrictions()
        {
            foreach (var s in userSettings)
            {
                var rule = ScreenPrivileges.GetRestrictions(s.ScreenName);

                if (rule.DisableAdd) s.AdditionPrivileges = false;
                if (rule.DisableEdit) s.EditPrivileges = false;
                if (rule.DisableDelete) s.DeletePrivileges = false;
                if (rule.DisableView) s.ViewPrivileges = false;
            }
        }

        private void ToggleAllAdd(ChangeEventArgs e, string module)
        {
            bool value = e.Value?.ToString()?.ToLower() == "true";
            if (categorizedSettings.TryGetValue(module, out var screens))
            {
                foreach (var s in screens)
                {
                    if (!ScreenPrivileges.GetRestrictions(s.ScreenName).DisableAdd)
                        s.AdditionPrivileges = value;
                }
            }
        }

        private void ToggleAllEdit(ChangeEventArgs e, string module)
        {
            bool value = e.Value?.ToString()?.ToLower() == "true";
            if (categorizedSettings.TryGetValue(module, out var screens))
            {
                foreach (var s in screens)
                {
                    if (!ScreenPrivileges.GetRestrictions(s.ScreenName).DisableEdit)
                        s.EditPrivileges = value;
                }
            }
        }

        private void ToggleAllDelete(ChangeEventArgs e, string module)
        {
            bool value = e.Value?.ToString()?.ToLower() == "true";
            if (categorizedSettings.TryGetValue(module, out var screens))
            {
                foreach (var s in screens)
                {
                    if (!ScreenPrivileges.GetRestrictions(s.ScreenName).DisableDelete)
                        s.DeletePrivileges = value;
                }
            }
        }

        private void ToggleAllView(ChangeEventArgs e, string module)
        {
            bool value = e.Value?.ToString()?.ToLower() == "true";
            if (categorizedSettings.TryGetValue(module, out var screens))
            {
                foreach (var s in screens)
                {
                    if (!ScreenPrivileges.GetRestrictions(s.ScreenName).DisableView)
                        s.ViewPrivileges = value;
                }
            }
        }

        private async Task SubmitUserSettings()
        {
            if (selectedUserId == null || selectedUserId == 0)
            {
                Notifier.Warning("Select User", "Please choose a user before submitting.");
                return;
            }

            IsSubmitting = true;

            try
            {
                var request = new
                {
                    UserId = selectedUserId,
                    Items = userSettings // this is your in-memory grid data
                };

                var response = await Http.PutAsJsonAsync("Admin/UpdateUserSettings", request);

                if (response.IsSuccessStatusCode)
                {
                    Notifier.Success("Success", "User settings updated successfully.");

                    originalUserSettings = userSettings
                        .Select(u => CloneUserSettingsDto(u))
                        .ToList();

                    CategorizeUserSettings();
                    ApplyRestrictions();
                }
                else
                {
                    var message = await response.Content.ReadAsStringAsync();
                    Notifier.Error("Update Failed", $"Server returned error: {message}");
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", $"An error occurred: {ex.Message}");
            }

            IsSubmitting = false;
        }

        private void ResetGrid()
        {
            if (originalUserSettings == null || !originalUserSettings.Any())
            {
                Notifier.Warning("No Data", "No data to reset.");
                return;
            }

            // Deep clone from backup
            userSettings = originalUserSettings
                .Select(u => CloneUserSettingsDto(u))
                .ToList();

            CategorizeUserSettings();
            ApplyRestrictions();
            StateHasChanged();

            Notifier.Info("Reset", "All changes reverted to original state.");
        }

        private UserSettingsDto CloneUserSettingsDto(UserSettingsDto src) => new()
        {
            ScreenName = src.ScreenName,
            AdditionPrivileges = src.AdditionPrivileges,
            EditPrivileges = src.EditPrivileges,
            DeletePrivileges = src.DeletePrivileges,
            ViewPrivileges = src.ViewPrivileges,
            Priority = src.Priority
        };

    }
}
