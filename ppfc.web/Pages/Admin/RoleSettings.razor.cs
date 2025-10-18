using Microsoft.AspNetCore.Components;
using ppfc.DTO;
using ppfc.web.Helpers;
using static ppfc.web.Helpers.ScreenPrivileges;
using static System.Net.WebRequestMethods;

namespace ppfc.web.Pages.Admin
{
    public partial class RoleSettings
    {
        [Inject] protected AppNotifier Notifier { get; set; } = default!;

        public List<RoleDto> roles = new();
        public List<RoleSettingsDto> roleSettings = new();
        private List<RoleSettingsDto> originalRoleSettings = new(); // Store original state
        public bool isLoading = true;
        public bool IsSubmitting = false;
        protected int? selectedRoleId;
        private int? originalSelectedRoleId;
        protected string? selectedPage; // Default Page dropdown
        private string? originalSelectedPage;

        public List<string> defaultPages = new()
        {
            "Account Close","Admin","Master","HP Return","Audit Finance","Audit Vehicle",
            "Audit RTO Transferred","Camp Charges","HP Entry","Audit Address","Finance Voucher",
            "Party Debit Entry","Complaint","Pending Account","Receipt","Rectification",
            "Bank","Party Credits","Vehicle Seize"
        };

        protected string SelectedRoleName =>
            roles.FirstOrDefault(r => r.RoleId == selectedRoleId)?.RoleName ?? "None";

        private string activeModule = "Admin"; // Default tab

        private void SelectTab(string module)
        {
            activeModule = module;
        }

        protected override async Task OnInitializedAsync()
        {
            try
            {
                roles = await Http.GetFromJsonAsync<List<RoleDto>>("Admin/GetRoles");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to load roles: {ex.Message}");
            }
            finally
            {
                isLoading = false;
            }
        }

        // Called when a role is selected
        protected async Task OnRoleChanged(ChangeEventArgs e)
        {
            if (int.TryParse(e.Value?.ToString(), out int roleId))
            {
                selectedRoleId = roleId;

                try
                {
                    // Call your API to get default page for this role
                    var response = await Http.GetFromJsonAsync<ScreenDto>($"Admin/GetDefaultPageByRole/{roleId}");
                    selectedPage = response?.ScreenName;

                    roleSettings = await Http.GetFromJsonAsync<List<RoleSettingsDto>>($"Admin/GetRoleSettings/{roleId}"
                    ) ?? new List<RoleSettingsDto>();

                    CategorizeRoleSettings();

                    ApplyRestrictions();

                    // Save original state for reset
                    originalSelectedRoleId = selectedRoleId;
                    originalSelectedPage = selectedPage;
                    originalRoleSettings = roleSettings.Select(rs => CloneRoleSettingsDto(rs)).ToList();
                }
                catch
                {
                    selectedPage = null;
                }
            }
        }

        // Updating Role Settings
        private async Task SubmitRoleSettings()
        {
            IsSubmitting = true;

            if (selectedRoleId == null || string.IsNullOrEmpty(selectedPage))
            {
                Notifier.Warning("Invalid Data", "Please select a role and default page before submitting.");
                IsSubmitting = false;
                return;
            }

            var request = new
            {
                RoleId = selectedRoleId,
                DefaultPage = selectedPage,
                Items = roleSettings
            };

            var response = await Http.PutAsJsonAsync("Admin/UpdateRoleSettings", request);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Role settings updated successfully.");
                originalRoleSettings = roleSettings.Select(rs => CloneRoleSettingsDto(rs)).ToList();

                originalSelectedPage = selectedPage;
                originalSelectedRoleId = selectedRoleId;

                // Optional: Refresh categorized dictionary too
                CategorizeRoleSettings();
                ApplyRestrictions();

                Notifier.Success("Updated Successfully", "Role settings saved successfully.");

            }
            else
                Notifier.Error("Update Failed", "Failed to update role settings.");

            IsSubmitting = false;
        }

        // Reset button
        private void ResetGrid()
        {
            selectedRoleId = originalSelectedRoleId;
            selectedPage = originalSelectedPage;
            roleSettings = originalRoleSettings.Select(rs => CloneRoleSettingsDto(rs)).ToList();

            CategorizeRoleSettings();
            ApplyRestrictions();
            StateHasChanged();
        }

        // Deep copy helper
        private RoleSettingsDto CloneRoleSettingsDto(RoleSettingsDto src) => new RoleSettingsDto
        {
            ScreenName = src.ScreenName,
            AdditionPrivileges = src.AdditionPrivileges,
            EditPrivileges = src.EditPrivileges,
            DeletePrivileges = src.DeletePrivileges,
            ViewPrivileges = src.ViewPrivileges
        };

        private void ApplyRestrictions()
        {
            foreach (var r in roleSettings)
            {
                var rule = ScreenPrivileges.GetRestrictions(r.ScreenName);

                if (rule.DisableAdd) r.AdditionPrivileges = false;
                if (rule.DisableEdit) r.EditPrivileges = false;
                if (rule.DisableDelete) r.DeletePrivileges = false;
                if (rule.DisableView) r.ViewPrivileges = false;
            }
        }

        #region Toogle Checkboxes for Module

        private void ToggleAllAdd(ChangeEventArgs e, string moduleName)
        {
            bool value = e.Value?.ToString()?.ToLower() == "true";

            if (categorizedRoles.TryGetValue(moduleName, out var screens))
            {
                foreach (var r in screens)
                {
                    var restriction = ScreenPrivileges.GetRestrictions(r.ScreenName);
                    if (!restriction.DisableAdd)
                        r.AdditionPrivileges = value;
                }
            }

            StateHasChanged();
        }

        private void ToggleAllEdit(ChangeEventArgs e, string moduleName)
        {
            bool value = e.Value?.ToString()?.ToLower() == "true";

            if (categorizedRoles.TryGetValue(moduleName, out var screens))
            {
                foreach (var r in screens)
                {
                    var restriction = ScreenPrivileges.GetRestrictions(r.ScreenName);
                    if (!restriction.DisableEdit)
                        r.EditPrivileges = value;
                }
            }

            StateHasChanged();
        }

        private void ToggleAllDelete(ChangeEventArgs e, string moduleName)
        {
            bool value = e.Value?.ToString()?.ToLower() == "true";

            if (categorizedRoles.TryGetValue(moduleName, out var screens))
            {
                foreach (var r in screens)
                {
                    var restriction = ScreenPrivileges.GetRestrictions(r.ScreenName);
                    if (!restriction.DisableDelete)
                        r.DeletePrivileges = value;
                }
            }

            StateHasChanged();
        }

        private void ToggleAllView(ChangeEventArgs e, string moduleName)
        {
            bool value = e.Value?.ToString()?.ToLower() == "true";

            if (categorizedRoles.TryGetValue(moduleName, out var screens))
            {
                foreach (var r in screens)
                {
                    var restriction = ScreenPrivileges.GetRestrictions(r.ScreenName);
                    if (!restriction.DisableView)
                        r.ViewPrivileges = value;
                }
            }

            StateHasChanged();
        }

        #endregion

        

        #region Backup for Rolesettings Categorization

        //// Tracks the current color as we iterate through screens
        //private string currentColor = "darkblue"; // default = Master Module

        //// Returns the color for each screen, based on trigger points
        //private string GetScreenColor(string screenName)
        //{
        //    if (screenName == "Adjustment Days")
        //        currentColor = "chocolate"; // Admin Module
        //    else if (screenName == "Area")
        //        currentColor = "darkblue"; // Master Module
        //    else if (screenName == "AC Close")
        //        currentColor = "red"; // Transaction Module
        //    else if (screenName == "ATC Audit")
        //        currentColor = "darkgreen"; // Audit Module
        //    else if (screenName == "ACClose HPReturn Report")
        //        currentColor = "black"; // Reports

        //    return currentColor;
        //}

        //// Returns readable module name based on color
        //private string GetModuleName(string color) => color switch
        //{
        //    "chocolate" => "Admin Module",
        //    "darkblue" => "Master Module",
        //    "red" => "Transaction Module",
        //    "darkgreen" => "Audit Module",
        //    "black" => "Reports",
        //    _ => ""
        //};


        //// Tracks the current color and module as we iterate
        //private string currentColor = "darkblue";
        //private string currentModule = "Master Module";

        //// Mapping of trigger screens to their color/module
        //private readonly Dictionary<string, (string color, string module)> colorTriggers = new()
        //{
        //    ["Adjustment Days"] = ("chocolate", "Admin Module"),
        //    ["Area"] = ("darkblue", "Master Module"),
        //    ["AC Close"] = ("red", "Transaction Module"),
        //    ["ATC Audit"] = ("darkgreen", "Audit Module"),
        //    ["ACClose HPReturn Report"] = ("black", "Reports")
        //};

        //// Checks and updates when a new module starts
        //private (string color, string module, bool isNewModule) GetScreenCategory(string screenName)
        //{
        //    bool isNew = false;

        //    if (colorTriggers.TryGetValue(screenName.Trim(), out var category))
        //    {
        //        currentColor = category.color;
        //        currentModule = category.module;
        //        isNew = true;
        //    }

        //    return (currentColor, currentModule, isNew);
        //}

        #endregion

        #region Role Settings Categorization - New Version

        // Define trigger points for module start (same as before)
        private readonly Dictionary<string, (string color, string module)> colorTriggers = new()
        {
            ["Adjustment Days"] = ("chocolate", "Admin"),
            ["Area"] = ("darkblue", "Master"),
            ["AC Close"] = ("red", "Transaction"),
            ["ATC Audit"] = ("darkgreen", "Audit"),
            ["ACClose HPReturn Report"] = ("black", "Reports")
        };

        // Maintain current color/module while looping
        private string currentModule = "Master";
        private string currentColor = "darkblue";

        // Categorized module data
        private Dictionary<string, List<RoleSettingsDto>> categorizedRoles = new();

        private void CategorizeRoleSettings()
        {
            categorizedRoles.Clear();
            currentModule = "Master";
            currentColor = "darkblue";

            foreach (var r in roleSettings)
            {
                // Check if this screen starts a new module
                if (colorTriggers.TryGetValue(r.ScreenName.Trim(), out var category))
                {
                    currentModule = category.module;
                    currentColor = category.color;
                }

                if (!categorizedRoles.ContainsKey(currentModule))
                    categorizedRoles[currentModule] = new List<RoleSettingsDto>();

                categorizedRoles[currentModule].Add(r);
            }
        }

        #endregion

    }
}
