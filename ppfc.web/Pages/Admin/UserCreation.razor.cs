using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using ppfc.DTO;
using ppfc.web.Helpers;
using ppfc.web.Pages.Master;
using Radzen;
using Radzen.Blazor;
using System.Data;
using System.Text.Json;
using static System.Net.WebRequestMethods;

namespace ppfc.web.Pages.Admin
{
    public partial class UserCreation
    {
        [Inject] private ProtectedSessionStorage SessionStorage { get; set; }
        [Inject] protected AppNotifier Notifier { get; set; } = default!;
        [Inject] UserContext UserContext { get; set; }
        [Inject] BreadcrumbService breadcrumbService { get; set; }

        private RadzenDataGrid<UserDto> grid;
        public List<UserDto> users = new();
        public List<RoleDto> roles = new();
        public List<EmployeeDto> employees = new();

        public bool IsLoading = true;
        public bool IsSubmitting = false;
        public int? EditLoadingUserId = null;
        public int? DelLoadingUserId = null;

        public int companyId { get; set; }
        public bool ShowPassword { get; set; } = false;
        public void Toggle() => ShowPassword = !ShowPassword;

        protected override async Task OnInitializedAsync()
        {
            //var result = await SessionStorage.GetAsync<int>("CompanyId");

            //// Assign it to your public property
            //companyId = result.Success ? result.Value : 0;

            companyId = UserContext.CompanyId;

            breadcrumbService.Set("Admin", "User Creation");

            try
            {
                roles = await Http.GetFromJsonAsync<List<RoleDto>>("Admin/GetRoles");
                users = await Http.GetFromJsonAsync<List<UserDto>>($"Admin/GetUsers/{companyId}");
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

        public async Task AddNew()
        {
            var newUser = new UserDto
            {
                UserId = 0,
                EmployeeId = 0,
                RoleId = 0
            };

            employees = await Http.GetFromJsonAsync<List<EmployeeDto>>($"Admin/GetEmployees/{companyId}");
            users.Insert(0, newUser);
            await grid.InsertRow(newUser);
        }

        public async Task EditUser(UserDto user)
        {
            EditLoadingUserId = user.UserId;
            employees = await Http.GetFromJsonAsync<List<EmployeeDto>>($"Admin/GetAvailableEmployees/{user.EmployeeName}");
            await grid.EditRow(user);
            await Task.Yield();
            EditLoadingUserId = null;
        }

        public async Task CancelEdit(UserDto user)
        {
            grid.CancelEditRow(user);

            // if newly added empty row cancelled, remove it
            if (string.IsNullOrEmpty(user.EmployeeName) && string.IsNullOrEmpty(user.UserName) && string.IsNullOrEmpty(user.RoleName))
            {
                users.Remove(user);
                await grid.Reload();
            }

            StateHasChanged();
        }

        public async Task DeleteUser(UserDto user)
        {
            if (user == null || user.UserId == 0)
            {
                Notifier.Warning("Invalid Selection", "Please select a valid user to delete.");
                return;
            }

            DelLoadingUserId = user.UserId;

            // 🟢 Show Radzen confirmation dialog
            bool? confirm = await DialogService.Confirm(
                $"Are you sure you want to delete '{user.UserName}'?",
                "Confirm Delete",
                new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
            );

            // confirm will be true if user clicked "Yes"
            if (confirm != true)
            {
                DelLoadingUserId = null;
                return;
            }               

            try
            {
                var response = await Http.DeleteAsync($"Admin/DeleteUser/{user.UserId}");

                if (response.IsSuccessStatusCode)
                {
                    users.Remove(user);
                    await grid.Reload();
                    Notifier.Success("Deleted Successfully", "User has been deleted successfully.");
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Delete Failed", message);
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", $"An error occurred while deleting: {ex.Message}");
            }
            DelLoadingUserId = null;
        }

        // ADDED: Validation method
        private bool ValidateUser(UserDto user)
        {
            if (user.EmployeeId == 0 || user.EmployeeId == null)
            {
                Notifier.Warning("Validation Error", "Please select an employee.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(user.UserName))
            {
                Notifier.Warning("Validation Error", "Username is required.");
                return false;
            }

            if (user.RoleId == 0 || user.RoleId == null)
            {
                Notifier.Warning("Validation Error", "Please select a role.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(user.Password))
            {
                Notifier.Warning("Validation Error", "Password is required.");
                return false;
            }

            //if (user.Password.Length < 6)
            //{
            //    Notifier.Warning("Validation Error", "Password must be at least 6 characters long.");
            //    return false;
            //}

            if (string.IsNullOrWhiteSpace(user.ConfirmPassword))
            {
                Notifier.Warning("Validation Error", "Please confirm your password.");
                return false;
            }

            if (user.Password != user.ConfirmPassword)
            {
                Notifier.Warning("Password Mismatch", "The password and confirm password do not match.");
                return false;
            }

            return true;
        }

        public async Task SaveUser(UserDto user)
        {

            IsSubmitting = true;

            if (user == null)
            {
                Notifier.Warning("Invalid Data", "Please select a user to update.");
                IsSubmitting = false;
                return;
            }

            if (!ValidateUser(user))
            {
                IsSubmitting = false;
                return;
            }

            try
            {
                HttpResponseMessage response;

                // Added: Check whether it's a new user (UserId == 0 means "Add New")
                if (user.UserId == 0)
                {
                    response = await Http.PostAsJsonAsync("Admin/CreateUser", user);
                }
                else
                {
                    response = await Http.PutAsJsonAsync("Admin/UpdateUser", user);
                }

                if (response.IsSuccessStatusCode)
                {
                    if (user.UserId == 0)
                    {
                        Notifier.Success("Created Successfully", "New user added successfully.");
                    }
                    else
                    {
                        Notifier.Success("Updated Successfully", "User details updated successfully.");
                    }

                    users = await Http.GetFromJsonAsync<List<UserDto>>($"Admin/GetUsers/{companyId}");
                    await grid.Reload();
                }
                else
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    string message = json.GetProperty("message").GetString();
                    Notifier.Error("Failed!", message);
                }
            }
            catch (Exception ex)
            {
                Notifier.Error("Error", $"An error occurred: {ex.Message}");
            }

            IsSubmitting = false;

        }
        
    }
}