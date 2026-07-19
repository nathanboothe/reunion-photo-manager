using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReunionPhotos.Web.Services;

namespace ReunionPhotos.Web.Pages;

public class LoginModel : PageModel
{
    private readonly PinAuthService _pinAuth;

    public LoginModel(PinAuthService pinAuth)
    {
        _pinAuth = pinAuth;
    }

    [BindProperty]
    public string Pin { get; set; } = "";

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Rate-limit by caller IP rather than by PIN, so one guessed PIN
        // doesn't let someone burn through the whole family list.
        var clientKey = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (_pinAuth.IsLockedOut(clientKey))
        {
            ErrorMessage = "Too many attempts. Please try again in a few minutes.";
            return Page();
        }

        var member = await _pinAuth.ValidatePinAsync(Pin, clientKey);
        if (member is null)
        {
            ErrorMessage = "That PIN wasn't recognized. Please try again.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, member.Id),
            new(ClaimTypes.Name, member.Name),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Login");
    }
}
