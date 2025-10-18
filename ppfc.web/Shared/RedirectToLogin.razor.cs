using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace ppfc.web.Shared
{
    public partial class RedirectToLogin : ComponentBase
    {
        [Inject] public NavigationManager Navigation { get; set; }
        [Inject] public AuthenticationStateProvider AuthStateProvider { get; set; }

        protected override async Task OnInitializedAsync()
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            // If not logged in, redirect immediately
            if (user.Identity is null || !user.Identity.IsAuthenticated)
            {
                var currentUrl = Navigation.Uri.ToLowerInvariant();

                // ✅ Prevent redirect loop if already on /login
                if (!currentUrl.EndsWith("/login"))
                {
                    Navigation.NavigateTo("/login");
                }
            }
        }
    }
}
