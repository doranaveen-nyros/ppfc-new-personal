using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ProtectedLocalStorage _sessionStorage;
    private ClaimsPrincipal _cachedUser;

    public CustomAuthenticationStateProvider(ProtectedLocalStorage sessionStorage)
    {
        _sessionStorage = sessionStorage;
        _cachedUser = new ClaimsPrincipal(new ClaimsIdentity()); // anonymous by default
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // If we already have a cached user, return it
        if (_cachedUser.Identity != null && _cachedUser.Identity.IsAuthenticated)
        {
            return new AuthenticationState(_cachedUser);
        }

        try
        {
            // During prerendering, JS interop isn't available
            if (_sessionStorage is null)
                return new AuthenticationState(_cachedUser);

            var storedResult = await _sessionStorage.GetAsync<string>("JwtToken");
            var token = storedResult.Success ? storedResult.Value : null;

            if (string.IsNullOrEmpty(token))
            {
                _cachedUser = new ClaimsPrincipal(new ClaimsIdentity());
                return new AuthenticationState(_cachedUser);
            }

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var identity = new ClaimsIdentity(jwtToken.Claims, "jwt");
            _cachedUser = new ClaimsPrincipal(identity);

            return new AuthenticationState(_cachedUser);
        }
        catch
        {
            _cachedUser = new ClaimsPrincipal(new ClaimsIdentity());
            return new AuthenticationState(_cachedUser);
        }
    }

    public async Task MarkUserAsAuthenticatedAsync(string token)
    {
        await _sessionStorage.SetAsync("JwtToken", token);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var identity = new ClaimsIdentity(jwtToken.Claims, "jwt");
        _cachedUser = new ClaimsPrincipal(identity);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_cachedUser)));
    }

    public async Task MarkUserAsLoggedOutAsync()
    {
        await _sessionStorage.DeleteAsync("JwtToken");
        _cachedUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_cachedUser)));
    }
}
