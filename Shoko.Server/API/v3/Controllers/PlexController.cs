using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Plex;
using Shoko.Server.Plex;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class PlexController : BaseController
{
    public PlexController(ISettingsProvider settingsProvider) : base(settingsProvider) {}

    /// <summary>
    /// Get an OAuth2 authenticate url to authenticate the current user.
    /// </summary>
    /// <param name="callbackUrl">Callback url to forward the user back to the callback trampoline after they've authenticated.</param>
    /// <returns></returns>
    [HttpGet("Authenticate")]
    public ActionResult<string> GetOAuthRequestUrl([FromQuery] string callbackUrl = null)
    {
        return PlexHelper.GetForUser(User).GetAuthenticationURL(callbackUrl);
    }

    /// <summary>
    /// Check if the current user is currently authenticated against the plex api.
    /// </summary>
    /// <returns></returns>
    [HttpGet("IsAuthenticated")]
    public ActionResult<bool> CheckIfAuthenticated()
    {
        return PlexHelper.GetForUser(User).IsAuthenticated;
    }

    /// <summary>
    /// Invalidate and remove the current plex authentication token.
    /// </summary>
    /// <returns></returns>
    [HttpDelete("IsAuthenticated")]
    public ActionResult<bool> InvalidateToken()
    {
        return PlexHelper.GetForUser(User).InvalidateToken();
    }

    /// <summary>
    /// Show all available server for the current user.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    [HttpGet("AvailableServers")]
    public ActionResult<List<PlexServer>> GetAvailableServers(int userId)
    {
        var helper = PlexHelper.GetForUser(User);
        var currentServerID = helper.SelectedServer?.ClientIdentifier ?? "";
        return helper.GetAllServers()
            .Select(device => new PlexServer(device, device.ClientIdentifier == currentServerID))
            .ToList();
    }

    /// <summary>
    /// Get the selected server for the current user.
    /// </summary>
    /// <returns></returns>
    [HttpGet("SelectedServer")]
    public ActionResult<PlexServer> GetServer()
    {
        var helper = PlexHelper.GetForUser(User);
        var currentServer = helper.SelectedServer;
        if (currentServer == null)
            return NoContent();

        return new PlexServer(currentServer, true);
    }

    /// <summary>
    /// Change/set the selected server for the current user.
    /// </summary>
    /// <param name="server">The new selected server.</param>
    /// <returns></returns>
    [HttpPut("SelectedServer")]
    public ActionResult<PlexServer> SelectServer(PlexServer server)
    {
        var helper = PlexHelper.GetForUser(User);
        var selectedServer = helper.GetAllServers()
            .FirstOrDefault(ser => ser.ClientIdentifier == server.ID);

        if (selectedServer == null)
            return BadRequest("");

        try {
            helper.UseServer(selectedServer);
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }

        return new PlexServer(selectedServer, true);
    }

    /// <summary>
    /// Unselect the server for the current user.
    /// </summary>
    /// <returns></returns>
    [HttpDelete("SelectedServer")]
    public ActionResult UnselectServer()
    {
        try {
            PlexHelper.GetForUser(User).UseServer(null);
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }

        return NoContent();
    }

    /// <summary>
    /// Display all the libraries available in the selected server for the
    /// current user.
    /// </summary>
    /// <returns></returns>
    [HttpGet("SelectedServer/Libraries")]
    public ActionResult<List<PlexLibrary>> GetLibraries()
    {
        try
        {
            var helper = PlexHelper.GetForUser(User);
            var currentServer = helper.SelectedServer;
            var directories = helper.GetDirectories();
            return directories
                .Select(directory => new PlexLibrary(directory, currentServer))
                .ToList();
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }

    /// <summary>
    /// Select the libraries used for the plex syncing for the current user.
    /// </summary>
    /// <param name="libraries"></param>
    /// <returns></returns>
    [HttpPut("SelectedServer/Libraries")]
    public ActionResult<List<PlexLibrary>> SelectLibraries([FromBody] List<PlexLibrary> libraries)
    {
        try
        {
            var helper = PlexHelper.GetForUser(User);
            var selectedServer = helper.SelectedServer;
            var selectedIDs = libraries
                .Select(library => library.ID)
                .ToHashSet();
            return helper.SetSelectedDirectories(selectedIDs)
                .Select(directory => new PlexLibrary(directory, selectedServer, selectedIDs.Contains(directory.Key)))
                .ToList();
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }
    /// <summary>
    /// Get an OAuth2 authenticate url to authenticate the user.
    /// </summary>
    /// <param name="userID">User ID</param>
    /// <param name="callbackUrl">Callback url to forward the user back to the callback trampoline after they've authenticated.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("{userID}/Authenticate")]
    public ActionResult<string> GetOAuthRequestUrlForUser([FromRoute] int userID, [FromQuery] string callbackUrl = null)
    {
        var user = RepoFactory.JMMUser.GetByID(userID);
        if (user == null)
            return NotFound("Unable to find user with the given id.");

        return PlexHelper.GetForUser(user).GetAuthenticationURL(callbackUrl);
    }

    /// <summary>
    /// Check if the user is currently authenticated against the plex api.
    /// </summary>
    /// <param name="userID">User ID</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("{userID}/IsAuthenticated")]
    public ActionResult<bool> CheckIfAuthenticatedForUser([FromRoute] int userID)
    {
        var user = RepoFactory.JMMUser.GetByID(userID);
        if (user == null)
            return NotFound("Unable to find user with the given id.");

        return PlexHelper.GetForUser(user).IsAuthenticated;
    }

    /// <summary>
    /// Invalidate and remove the plex authentication token.
    /// </summary>
    /// <param name="userID">User ID</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("{userID}/IsAuthenticated")]
    public ActionResult<bool> InvalidateTokenForUser([FromRoute] int userID)
    {
        var user = RepoFactory.JMMUser.GetByID(userID);
        if (user == null)
            return NotFound("Unable to find user with the given id.");

        return PlexHelper.GetForUser(user).InvalidateToken();
    }

    /// <summary>
    /// Show all available server for the user.
    /// </summary>
    /// <param name="userID">User ID</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("{userID}/AvailableServers")]
    public ActionResult<List<PlexServer>> GetAvailableServersForUser([FromRoute] int userID)
    {
        var user = RepoFactory.JMMUser.GetByID(userID);
        if (user == null)
            return NotFound("Unable to find user with the given id.");

        var helper = PlexHelper.GetForUser(user);
        var currentServerID = helper.SelectedServer?.ClientIdentifier ?? "";
        return helper.GetAllServers()
            .Select(device => new PlexServer(device, device.ClientIdentifier == currentServerID))
            .ToList();
    }

    /// <summary>
    /// Get the selected server for the user.
    /// </summary>
    /// <param name="userID">User ID</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("{userID}/SelectedServer")]
    public ActionResult<PlexServer> GetServerForUser([FromRoute] int userID)
    {
        var user = RepoFactory.JMMUser.GetByID(userID);
        if (user == null)
            return NotFound("Unable to find user with the given id.");

        var helper = PlexHelper.GetForUser(user);
        var currentServer = helper.SelectedServer;
        if (currentServer == null)
            return NoContent();

        return new PlexServer(currentServer, true);
    }

    /// <summary>
    /// Change/set the selected server for the user.
    /// </summary>
    /// <param name="userID">User ID</param>
    /// <param name="server">The new selected server.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPut("{userID}/SelectedServer")]
    public ActionResult<PlexServer> SelectServerForUser([FromRoute] int userID, PlexServer server)
    {
        var user = RepoFactory.JMMUser.GetByID(userID);
        if (user == null)
            return NotFound("Unable to find user with the given id.");

        var helper = PlexHelper.GetForUser(user);
        var selectedServer = helper.GetAllServers()
            .FirstOrDefault(ser => ser.ClientIdentifier == server.ID);

        if (selectedServer == null)
            return BadRequest("");

        try {
            helper.UseServer(selectedServer);
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }

        return new PlexServer(selectedServer, true);
    }

    /// <summary>
    /// Unselect the server for the user.
    /// </summary>
    /// <param name="userID">User ID</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("{userID}/SelectedServer")]
    public ActionResult UnselectServerForUser([FromRoute] int userID)
    {
        var user = RepoFactory.JMMUser.GetByID(userID);
        if (user == null)
            return NotFound("Unable to find user with the given id.");

        var helper = PlexHelper.GetForUser(user);
        try {
            helper.UseServer(null);
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }

        return NoContent();
    }

    /// <summary>
    /// Display all the libraries available in the selected server for the user.
    /// </summary>
    /// <param name="userID">User ID</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("{userID}/SelectedServer/Libraries")]
    public ActionResult<List<PlexLibrary>> GetLibrariesForUser([FromRoute] int userID)
    {
        var user = RepoFactory.JMMUser.GetByID(userID);
        if (user == null)
            return NotFound("Unable to find user with the given id.");

        var helper = PlexHelper.GetForUser(user);
        try
        {
            var currentServer = helper.SelectedServer;
            var directories = helper.GetDirectories();
            return directories
                .Select(directory => new PlexLibrary(directory, currentServer))
                .ToList();
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }

    /// <summary>
    /// Select the libraries used for the plex syncing for the user.
    /// </summary>
    /// <param name="userID">User ID</param>
    /// <param name="libraries"></param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPut("{userID}/SelectedServer/Libraries")]
    public ActionResult<List<PlexLibrary>> SelectLibrariesForUser([FromRoute] int userID, [FromBody] List<PlexLibrary> libraries)
    {
        var user = RepoFactory.JMMUser.GetByID(userID);
        if (user == null)
            return NotFound("Unable to find user with the given id.");

        var helper = PlexHelper.GetForUser(user);
        try
        {
            var selectedServer = helper.SelectedServer;
            var selectedIDs = libraries
                .Select(library => library.ID)
                .ToHashSet();
            return helper.SetSelectedDirectories(selectedIDs)
                .Select(directory => new PlexLibrary(directory, selectedServer, selectedIDs.Contains(directory.Key)))
                .ToList();
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }
}
