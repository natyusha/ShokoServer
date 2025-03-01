using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize(Roles = "admin,init")]
[DatabaseBlockedExempt]
[InitFriendly]
public class SettingsController : BaseController
{
    private readonly IConnectivityService _connectivityService;

    // As far as I can tell, only GET and PATCH should be supported, as we don't support unset settings.
    // Some may be patched to "", though.

    // TODO some way of distinguishing what a normal user vs an admin can set.

    /// <summary>
    /// Get all settings
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<IServerSettings> GetSettings()
    {
        return new ActionResult<IServerSettings>(SettingsProvider.GetSettings());
    }

    /// <summary>
    /// JsonPatch the settings
    /// </summary>
    /// <param name="settings">JsonPatch operations</param>
    /// <param name="skipValidation">Skip Model Validation. Use with caution</param>
    /// <returns></returns>
    [HttpPatch]
    public ActionResult SetSettings([FromBody] JsonPatchDocument<ServerSettings> settings, bool skipValidation = false)
    {
        if (settings == null)
        {
            return ValidationProblem("The settings object is invalid.");
        }

        var existingSettings = SettingsProvider.GetSettings(copy: true);
        settings.ApplyTo((ServerSettings)existingSettings, ModelState);
        if (!skipValidation && !TryValidateModel(existingSettings))
            return ValidationProblem(ModelState);

        SettingsProvider.SaveSettings(existingSettings);
        return Ok();
    }

    /// <summary>
    /// Tests a Login with the given Credentials. This does not save the credentials.
    /// </summary>
    /// <param name="credentials">POST the body as a <see cref="Credentials"/> object</param>
    /// <returns></returns>
    [HttpPost("AniDB/TestLogin")]
    public ActionResult TestAniDB([FromBody] Credentials credentials)
    {
        if (string.IsNullOrWhiteSpace(credentials.Username))
            ModelState.AddModelError(nameof(credentials.Username), "Username cannot be empty.");

        if (string.IsNullOrWhiteSpace(credentials.Password))
            ModelState.AddModelError(nameof(credentials.Password), "Password cannot be empty.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var handler = HttpContext.RequestServices.GetRequiredService<IUDPConnectionHandler>();
        handler.ForceLogout();

        if (!handler.TestLogin(credentials.Username, credentials.Password))
            return ValidationProblem("Failed to log in.", "Connection");

        return Ok();
    }

    /// <summary>
    /// Gets the current network connectivity details for the server.
    /// </summary>
    /// <returns></returns>
    [HttpGet("Connectivity")]
    public ActionResult<ConnectivityDetails> GetNetworkAvailability()
    {
        return new ConnectivityDetails(_connectivityService);
    }

    /// <summary>
    /// Forcefully re-checks the current network connectivity, then returns the
    /// updated details for the server.
    /// </summary>
    /// <returns></returns>
    [HttpPost("Connectivity")]
    public async Task<ActionResult<object>> CheckNetworkAvailability()
    {
        await _connectivityService.CheckAvailability();

        return GetNetworkAvailability();
    }

    public SettingsController(ISettingsProvider settingsProvider, IConnectivityService connectivityService) : base(settingsProvider)
    {
        _connectivityService = connectivityService;
    }
}
