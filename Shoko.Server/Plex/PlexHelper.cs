using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Models.Plex;
using Shoko.Models.Plex.Connections;
using Shoko.Models.Plex.Login;
using Shoko.Server.Models;
using Shoko.Server.Plex.Libraries;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using MediaContainer = Shoko.Models.Plex.Connections.MediaContainer;

namespace Shoko.Server.Plex;

public class PlexHelper
{
    private const string ClientIdentifier = "d14f0724-a4e8-498a-bb67-add795b38331";

    private static readonly HttpClient HttpClient = new();

    private static readonly ConcurrentDictionary<int, PlexHelper> Cache = new();

    // We cache the user id and not the user so we pick up on user updates over
    // the life-span of the helper.
    private readonly int UserID;

    private readonly ILogger<PlexHelper> Logger = Utils.ServiceContainer.GetService(typeof(ILogger<PlexHelper>)) as ILogger<PlexHelper>;

    private SVR_JMMUser User
        => RepoFactory.JMMUser.GetByID(UserID);

    internal readonly JsonSerializerSettings SerializerSettings = new();

    private Connection _cachedConnection;
    private PlexKey _key;
    private DateTime _lastCacheTime = DateTime.MinValue;
    private DateTime _lastMediaCacheTime = DateTime.MinValue;

    private List<MediaDevice> __allDevices;

    private MediaDevice __currentServer;

    private bool? __isAuthenticated;

    private User __plexUser = null;

    private PlexHelper(int userID) : base()
    {
        UserID = userID;
        SerializerSettings.Converters.Add(new PlexConverter(this));
        SetupHttpClient(HttpClient, TimeSpan.FromSeconds(60));
    }

    public MediaDevice SelectedServer
    {
        get
        {
            var settings = User.Plex;
            if (string.IsNullOrEmpty(settings.SelectedServer))
            {
                return null;
            }

            if (DateTime.Now - TimeSpan.FromHours(1) >= _lastMediaCacheTime)
            {
                __currentServer = null;
            }

            if (__currentServer != null && settings.SelectedServer == __currentServer.ClientIdentifier)
            {
                return __currentServer;
            }

            __currentServer = GetAllServers()
                .FirstOrDefault(s => s.ClientIdentifier == settings.SelectedServer);
            if (__currentServer != null)
            {
                _lastMediaCacheTime = DateTime.Now;
                return __currentServer;
            }

            // Also check ip and port.
            if (settings.SelectedServer.Contains(':'))
            {
                var lastColon = settings.SelectedServer.LastIndexOf(':');
                var address = settings.SelectedServer[0..lastColon];
                var port = settings.SelectedServer[(lastColon + 1)..];

                // Basic validation of the port.
                var isValidPort = int.TryParse(port, NumberStyles.Integer, null, out var _);
                if (!isValidPort)
                    return null;

                __currentServer = GetAllServers().FirstOrDefault(server => server.Connection.Any(c => c.Address == address && c.Port == port));
                if (__currentServer != null)
                {
                    _lastMediaCacheTime = DateTime.Now;
                    settings.SelectedServer = __currentServer.ClientIdentifier;
                    RepoFactory.JMMUser_Plex.Save(settings);
                    return __currentServer;
                }
            }

            return null;
        }
        private set
        {
            __currentServer = value;
            _lastMediaCacheTime = DateTime.Now;
        }
    }

    private Connection ConnectionCache
    {
        get
        {
            if (DateTime.Now - TimeSpan.FromHours(12) < _lastCacheTime && _cachedConnection != null)
            {
                return _cachedConnection;
            }

            _cachedConnection = null;

            //foreach (var connection in ServerCache.Connection)
            Parallel.ForEach(SelectedServer.Connection, (connection, state) =>
                {
                    try
                    {
                        if (state.ShouldExitCurrentIteration)
                        {
                            return;
                        }

                        var (result, _) = Request($"{connection.Uri}/library/sections", HttpMethod.Get,
                                new Dictionary<string, string> { { "X-Plex-Token", SelectedServer.AccessToken } })
                            .Result;

                        if (result != HttpStatusCode.OK)
                        {
                            Logger.LogTrace($"Got response from: {connection.Uri} {result}");
                            return;
                        }

                        _cachedConnection = _cachedConnection ?? connection;
                        state.Break();
                    }
                    catch (AggregateException e)
                    {
                        Logger.LogTrace($"Failed connection to: {connection.Uri} {e}");
                    }
                }
            );

            _lastCacheTime = DateTime.Now;

            return _cachedConnection;
        }
    }

    private Dictionary<string, string> AuthenticationHeaders => new() { { "X-Plex-Token", GetPlexToken() } };

    public bool IsAuthenticated
    {
        get
        {
            if (__isAuthenticated.HasValue && __isAuthenticated.Value)
                return true;

            try
            {
                var (status, body) = Request("https://plex.tv/users/account.json", HttpMethod.Get, AuthenticationHeaders)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
                __isAuthenticated = status == HttpStatusCode.OK;
                __plexUser = __isAuthenticated.Value ? JsonConvert.DeserializeObject<PlexAccount>(body).User : null;
                return __isAuthenticated.Value;
            }
            catch (Exception)
            {
                __isAuthenticated = null;
                __plexUser = null;
                return false;
            }
        }
        set
        {
            // Don't allow setting the value to true.
            if (value) return;
            __isAuthenticated = null;
            __plexUser = null;
        }
    }

    public User PlexUser
        => IsAuthenticated ? __plexUser : null;

    private static void SetupHttpClient(HttpClient client, TimeSpan timeout)
    {
        client.DefaultRequestHeaders.Add("X-Plex-Client-Identifier", ClientIdentifier);
        client.DefaultRequestHeaders.Add("X-Plex-Platform-Version", ServerState.Instance.ApplicationVersion);
        client.DefaultRequestHeaders.Add("X-Plex-Platform", "Shoko Server");
        client.DefaultRequestHeaders.Add("X-Plex-Device-Name", "Shoko Server Sync");
        client.DefaultRequestHeaders.Add("X-Plex-Product", "Shoko Server Sync");
        client.DefaultRequestHeaders.Add("X-Plex-Device", "Shoko");
        client.DefaultRequestHeaders.Add("User-Agent",
            $"{Assembly.GetEntryAssembly().GetName().Name} v${Assembly.GetEntryAssembly().GetName().Version}");
        client.Timeout = timeout;
    }

    /// <summary>
    /// Get a plex key for use with the OAuth2 authentication flow.
    /// </summary>
    /// <returns></returns>
    private PlexKey GetPlexKey()
    {
        if (_key != null)
        {
            if (_key.ExpiresAt > DateTime.Now)
            {
                return _key;
            }

            if (_key.ExpiresAt <= DateTime.Now)
            {
                _key = null;
            }
        }

        var (status, content) = Request("https://plex.tv/api/v2/pins?strong=true", HttpMethod.Post).Result;
        if (status != HttpStatusCode.OK)
            return _key = null;
        _key = JsonConvert.DeserializeObject<PlexKey>(content);
        return _key;
    }

    /// <summary>
    /// Get a plex api token for use with the plex api.
    /// </summary>
    /// <returns></returns>
    private string GetPlexToken()
    {
        var plexUserSettings = User.Plex;
        if (!string.IsNullOrEmpty(plexUserSettings.Token))
        {
            return plexUserSettings.Token;
        }

        if (_key == null)
        {
            GetPlexKey();
        }

        if (_key.AuthToken != null)
        {
            return _key?.AuthToken;
        }

        var (_, content) = Request($"https://plex.tv/api/v2/pins/{_key.Id}", HttpMethod.Get).Result;
        try
        {
            _key = JsonConvert.DeserializeObject<PlexKey>(content);
        }
        catch
        {
            Logger.LogTrace($"Unable to deserialize Plex Key from server. Response was \n{content}");
        }

        if (_key == null)
        {
            return null;
        }

        plexUserSettings.Token = _key.AuthToken;
        plexUserSettings.AccountID = _key.Id;
        RepoFactory.JMMUser_Plex.Save(plexUserSettings);
        return plexUserSettings.Token;
    }

    public static PlexHelper GetForUser(SVR_JMMUser user)
    {
        return Cache.GetOrAdd(user.JMMUserID, userID => new PlexHelper(userID));
    }

    /// <summary>
    /// Make an async request to plex
    /// </summary>
    /// <param name="status"></param>
    /// <param name="url"></param>
    /// <param name="method"></param>
    /// <param name="headers"></param>
    /// <param name="content"></param>
    /// <param name="xml"></param>
    /// <param name="configureRequest"></param>
    /// <returns></returns>
    private async Task<(HttpStatusCode status, string content)> Request(string url, HttpMethod method,
        IDictionary<string, string> headers = default, string content = null, bool xml = false,
        Action<HttpRequestMessage> configureRequest = null)
    {
        Logger.LogTrace($"Requesting from plex: {method.Method} {url}");
        var req = new HttpRequestMessage(method, url);
        if (method == HttpMethod.Post)
            req.Content = new StringContent(content ?? "");

        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(xml ? "application/xml" : "application/json"));
        if (headers != null)
        {
            foreach (var (header, val) in headers)
            {
                if (req.Headers.Contains(header))
                {
                    req.Headers.Remove(header);
                }

                req.Headers.Add(header, val);
            }
        }

        configureRequest?.Invoke(req);

        var response = await HttpClient.SendAsync(req).ConfigureAwait(false);

        // Invalidate the state if we recive an authorised response.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            __isAuthenticated = null;
            __plexUser = null;

            var plexUserSettings = User.Plex;
            plexUserSettings.AccountID = null;
            plexUserSettings.Token = null;
            RepoFactory.JMMUser_Plex.Save(plexUserSettings);
        }

        Logger.LogTrace($"Got response: {response.StatusCode}");
        return (response.StatusCode, await response.Content.ReadAsStringAsync().ConfigureAwait(false));
    }

    /// <summary>
    /// Request a resource from the selected server.
    /// </summary>
    /// <param name="path">Path to the resource to fetch.</param>
    /// <returns>The status and striungified contents.</returns>
    public Task<(HttpStatusCode status, string content)> RequestFromPlex(string path)
    {
        return Request($"{ConnectionCache.Uri}{path}", HttpMethod.Get,
                new Dictionary<string, string> { { "X-Plex-Token", SelectedServer.AccessToken } });
    }

    /// <summary>
    /// Get an OAuth2 autentication url, optional with a provided callback url
    /// for when the autentication is complete.
    /// </summary>
    /// <param name="forwardUrl"></param>
    /// <returns></returns>
    public string GetAuthenticationURL(string forwardUrl = null)
    {
        var url = $"https://app.plex.tv/auth#?clientID={ClientIdentifier}&code={GetPlexKey().Code}";
        if (!string.IsNullOrEmpty(forwardUrl))
            url += $"&forwardUrl=${HttpUtility.UrlEncode(forwardUrl)}";
        return url;
    }

    /// <summary>
    /// Get all available servers.
    /// </summary>
    /// <param name="force"></param>
    /// <returns></returns>
    public List<MediaDevice> GetAllServers(bool force = false)
    {
        if (!IsAuthenticated)
            throw new Exception($"User \"{User.Username}\" has not authenticated with plex yet. Authenticate first before retrying.");

        if (!force && __allDevices != null)
        {
            return __allDevices
                .Where(d => d.Provides.Split(',')
                .Contains("server"))
                .ToList();
        }

        var (_, content) = Request("https://plex.tv/api/resources?includeHttps=1", HttpMethod.Get,
            AuthenticationHeaders).Result;
        var serializer = new XmlSerializer(typeof(MediaContainer));
        using (TextReader reader = new StringReader(content))
            __allDevices = ((MediaContainer)serializer.Deserialize(reader))?.Device?.ToList() ?? new();

        return __allDevices
            .Where(d => d.Provides.Split(',').Contains("server"))
            .ToList();
    }

    /// <summary>
    /// Select the server to use.
    /// </summary>
    /// <param name="server">The new server to use.</param>
    public void UseServer(MediaDevice server)
    {
        var previousServer = SelectedServer;
        var settings = User.Plex;
        if (server == null)
        {
            // Update the settings if the server is not already unset.
            if (!string.IsNullOrEmpty(settings.SelectedServer))
            {
                settings.SelectedServer = null;
                settings.SelectedLibraries = new();
                RepoFactory.JMMUser_Plex.Save(settings);
            }
            return;
        }

        if (!IsAuthenticated)
            throw new Exception($"User \"{User.Username}\" has not authenticated with plex yet. Authenticate first before retrying.");

        if (server == null || !server.Provides.Split(',').Contains("server"))
            throw new Exception("Invalid server selection.");

        // Reset the selected libraries if we're switching server.
        settings.SelectedServer = server.ClientIdentifier;
        if (previousServer != null && server.ClientIdentifier != previousServer.ClientIdentifier)
        {
            settings.SelectedLibraries = new();
        }
        RepoFactory.JMMUser_Plex.Save(settings);
        SelectedServer = server;
    }

    /// <summary>
    /// Get the directories for the selected server. Returns an empty array if
    /// no server is currently selected.
    /// </summary>
    /// <returns>All the directories for the currently selected server.</returns>
    public SVR_Directory[] GetDirectories()
    {
        if (!IsAuthenticated)
            throw new Exception($"User \"{User.Username}\" has not authenticated with plex yet. Authenticate first before retrying.");

        if (SelectedServer == null)
            return new SVR_Directory[0];

        var (_, data) = RequestFromPlex("/library/sections").Result;
        return JsonConvert
            .DeserializeObject<MediaContainer<Shoko.Models.Plex.Libraries.MediaContainer>>(data, SerializerSettings)
            .Container.Directory?
            .Cast<SVR_Directory>()
            .ToArray() ?? new SVR_Directory[0];
    }

    /// <summary>
    /// Get only the selected directories for the selected server. Returns an
    /// empty array if no server is currently selected.
    /// </summary>
    /// <returns>The selected directories for the currently selected server.</returns>
    public SVR_Directory[] GetSelectedDirectories()
    {
        var plexUserSettings = User.Plex;
        return GetDirectories()
            .Where(dir => plexUserSettings.SelectedLibraries.Contains(dir.Key))
            .ToArray();
    }

    /// <summary>
    /// Update the directory selection.
    /// </summary>
    /// <param name="selection"></param>
    /// <returns>All the directories for the currently selected server.</returns>
    public SVR_Directory[] SetSelectedDirectories(HashSet<int> selection)
    {
        if (!IsAuthenticated)
            throw new Exception($"User \"{User.Username}\" has not authenticated with plex yet. Authenticate first before retrying.");

        // Sanity check #1 – Check if the user have selected a server.
        var currentServer = SelectedServer;
        if (currentServer == null)
            throw new Exception("A server has not been selected yet. Select a server first.");

        var directories = GetDirectories();
        var directorySet = directories
            .Select(directory => directory.Key)
            .ToHashSet();

        // Sanity check #2 – Check if the user have selected only valid directory keys.
        if (selection.Any(library => !directorySet.Contains(library)))
            throw new Exception("Invalid library selection.");

        var plexUserSettings = User.Plex;
        plexUserSettings.SelectedLibraries = selection;
        RepoFactory.JMMUser_Plex.Save(plexUserSettings);

        // Return all the directories.
        return GetDirectories();
    }

    /// <summary>
    /// Invalidate the current token for the user.
    /// </summary>
    /// <returns>True if the token was invalidated.</returns>
    public bool InvalidateToken()
    {
        if (!IsAuthenticated)
            return false;

        // Reset the cached in-memory values.
        __isAuthenticated = false;
        __plexUser = null;

        // Update the user's settings.
        var plexUserSettings = User.Plex;
        plexUserSettings.Token = null;
        RepoFactory.JMMUser_Plex.Save(plexUserSettings);

        // TODO: Invalidate the token on Plex's side if it's not expired.

        return true;
    }
}
