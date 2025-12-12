using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Components.Web;
using ppfc.DTO;
using ppfc.web.Helpers;
using static System.Net.WebRequestMethods;


namespace ppfc.web.Pages
{
    public partial class Login
    {
        [Inject] private ProtectedLocalStorage SessionStorage { get; set; }
        [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; }
        [Inject] private NavigationManager Navigation { get; set; }
        [Inject] protected AppNotifier Notifier { get; set; } = default!;
        [Inject] UserContext UserContext { get; set; }


        LoginRequestDto loginData = new();
        private List<NewsDto> news = new();

        string errorMessage;
        public bool ShowPassword = false;
        public bool IsSubmitting = false;

        protected override async Task OnInitializedAsync()
        {
            StateHasChanged(); // ensure loading screen appears

            try
            {
                news = await Http.GetFromJsonAsync<List<NewsDto>>("Login/GetNews");
                await Http.PostAsync("Login/LoadMethods", null);
            }
            finally
            {
                StateHasChanged(); // now render the login form
            }
        }

        public void TogglePassword() => ShowPassword = !ShowPassword;

        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(loginData.UserName))
            {
                Notifier.Warning("Username is required.");
                return;
            }
            if (string.IsNullOrWhiteSpace(loginData.Password))
            {
                Notifier.Warning("Password is required.");
                return;
            }

            errorMessage = null;
            IsSubmitting = true;
            StateHasChanged();

            try
            {
                var response = await Http.PostAsJsonAsync("Login/GetLogin", loginData);
                if (response.IsSuccessStatusCode)
                {
                    var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
                    if (loginResponse != null)
                    {
                        // Store JWT token in session storage (or local storage)
                        await SessionStorage.SetAsync("JwtToken", loginResponse.Token);

                        // Notify authentication provider (update auth state)
                        await ((CustomAuthenticationStateProvider)AuthStateProvider)
                             .MarkUserAsAuthenticatedAsync(loginResponse.Token);

                        var userInfo = loginResponse.LoginData;
                        await SessionStorage.SetAsync("UserId", userInfo.UserId);
                        await SessionStorage.SetAsync("RoleName", userInfo.RoleName);
                        await SessionStorage.SetAsync("CompanyId", userInfo.CompanyId);
                        await SessionStorage.SetAsync("BranchId", userInfo.BranchId);
                        await SessionStorage.SetAsync("CompanyCode", userInfo.CompanyCode);
                        await SessionStorage.SetAsync("CompanyName", userInfo.CompanyName);
                        await SessionStorage.SetAsync("UserName", userInfo.UserName);
                        await SessionStorage.SetAsync("Privileges", userInfo.Privileges);

                        // ✅ Store once globally for whole app
                        UserContext.SetUser(
                            userInfo.CompanyId,
                            userInfo.UserId,
                            userInfo.UserName
                        );

                        // Attach JWT to HttpClient for future requests
                        Http.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResponse.Token);

                        // Redirect to the Home page
                        Navigation.NavigateTo("/home");
                    }
                    else
                    {
                        errorMessage = "Invalid response from server.";
                        Notifier.Error("Login Failed", "Invalid response from server.");
                    }
                }
                else
                {
                    errorMessage = "Invalid username or password";
                    Notifier.Error("Login Failed", "Invalid username or password.");
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"An error occurred: {ex.Message}";
            }
            finally
            {
                IsSubmitting = false;
            }
        }
    }
}
