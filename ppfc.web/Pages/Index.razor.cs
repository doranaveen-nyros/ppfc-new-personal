using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace ppfc.web.Pages
{
    public partial class Index
    {
        [Inject] private NavigationManager Navigation { get; set; }

        private bool hasRedirected = false;

        protected override void OnAfterRender(bool firstRender)
        {
            if (firstRender && !hasRedirected)
            {
                hasRedirected = true;
                Navigation.NavigateTo("/login", forceLoad: true);
            }
        }      
    }
}
