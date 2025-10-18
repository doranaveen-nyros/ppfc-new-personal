using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace ppfc.web.Shared
{
    public partial class BlankLayout
    {
        public ErrorBoundary? ErrorB { get; set; }

        [JSInvokable("IsDebugMode")]
        public static bool IsDebugMode()
        {
#if DEBUG
            return true;
#else
    return false;
#endif
        }
        protected override async Task OnInitializedAsync()
        {
            ErrorB = new ErrorBoundary();

            await base.OnInitializedAsync();
        }
    }
}
