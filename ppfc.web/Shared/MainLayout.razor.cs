using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using ppfc.web.Helpers;

namespace ppfc.web.Shared
{
    public partial class MainLayout
    {
        bool sidebarExpanded = true;

        [Inject] private ProtectedLocalStorage SessionStorage { get; set; }
        [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; }
        [Inject] private NavigationManager Navigation { get; set; }
        [Inject] BreadcrumbService breadcrumbService { get; set; }

        protected override void OnInitialized()
        {
            breadcrumbService.OnChange += () => InvokeAsync(StateHasChanged);

        }

        private async Task Logout()
        {
            // Remove all keys stored at login
            var keys = new[] { "JwtToken", "UserId", "RoleName", "CompanyId", "BranchId", "CompanyCode", "CompanyName", "UserName", "Privileges" };
            foreach (var key in keys)
            {
                await SessionStorage.DeleteAsync(key);
            }

            // Notify authentication state provider
            if (AuthStateProvider is CustomAuthenticationStateProvider customAuth)
            {
                await customAuth.MarkUserAsLoggedOutAsync();
            }

            // Redirect to login page
            Navigation.NavigateTo("/login", forceLoad: true);
        }

        //protected override void OnInitialized()
        //{
        //    TitleService.TitleChanged += OnTitleChanged;
        //}

        private void OnTitleChanged()
        {
            InvokeAsync(StateHasChanged);
        }

        //public void Dispose()
        //{
        //    TitleService.TitleChanged -= OnTitleChanged;
        //}
    }
}
