using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FrostAura.Libraries.Core.Extensions.Decoration;
using FrostAura.Libraries.Core.Extensions.Validation;
using FrostAura.Libraries.Core.Models.Auth;
using FrostAura.Libraries.Http.Extensions;
using FrostAura.Libraries.Http.Interfaces;
using FrostAura.Libraries.Http.Models.Requests;
using FrostAura.Libraries.Http.Models.Responses;
using FrostAura.Libraries.Http.Services;
using FrostAura.Libraries.MediaServer.Core.Enums;
using FrostAura.Libraries.MediaServer.Core.Interfaces;
using FrostAura.Libraries.MediaServer.Core.Models.Content;
using MediaServer.Plex.Enums;
using MediaServer.Plex.Extensions;
using MediaServer.Plex.Interfaces;
using MediaServer.Plex.Models.Collections;
using MediaServer.Plex.Models.Config;
using MediaServer.Plex.Models.Content;
using MediaServer.Plex.Models.Requests;
using MediaServer.Plex.Models.Responses;

namespace MediaServer.Plex.Services
{
    /// <summary>
    /// Plex Media Server service.
    /// </summary>
    public class PlexMediaService : IMediaServer<PlexMediaServerConfig, Device>
    {
        /// <summary>
        /// Media server specific configuration. Should be set in a constructor.
        /// </summary>
        public PlexMediaServerConfig Configuration { get; }

        /// <summary>
        /// Instance of static http service to use in making web requests.
        /// </summary>
        private IHttpService _httpService { get; }
        
        /// <summary>
        /// Instance of authentication service to use for Plex.
        /// </summary>
        private IPlexAuthenticator _authenticator { get; }
        
        /// <summary>
        /// Instance of Plex server settings provider.
        /// </summary>
        private IPlexServerSettingsProvider _serverSettingsProvider { get; }

        /// <summary>
        /// Constructor for Plex headers.
        /// </summary>
        private IDictionary<string, string> _plexBasicHeaders { get; }

        /// <summary>
        /// Overloaded constructor to pass configuration.
        /// </summary>
        /// <param name="configuration">Media server specific configuration.</param>
        /// <param name="httpService">Instance of static http service to use in making web requests.</param>
        /// <param name="authenticator">Instance of static http service to use in making web requests.</param>
        /// <param name="serverSettingsProvider">Instance of Plex server settings provider.</param>
        /// <param name="mediaProvider">Instance of Plex server media provider.</param>
        public PlexMediaService(PlexMediaServerConfig configuration,
            IHttpService httpService,
            IPlexAuthenticator authenticator,
            IPlexServerSettingsProvider serverSettingsProvider,
            IHeaderConstructor<PlexBasicRequestHeaders> plexBasicHeadersConstructor)
        {
            Configuration = configuration
                .ThrowIfNull(nameof(configuration))
                .ThrowIfInvalid(nameof(configuration));
            _httpService = httpService
                .ThrowIfNull(nameof(httpService));
            _authenticator = authenticator
                .ThrowIfNull(nameof(authenticator));
            _serverSettingsProvider = serverSettingsProvider
                .ThrowIfNull(nameof(serverSettingsProvider));
            _plexBasicHeaders = plexBasicHeadersConstructor
                .ThrowIfNull(nameof(plexBasicHeadersConstructor))
                .ConstructRequestHeaders(configuration.BasicPlexHeaders);
        }

        /// <summary>
        /// Construct a default instance of the Plex service.
        /// </summary>
        /// <param name="username">Username used to get auth token.</param>
        /// <param name="password">Username used to get auth token.</param>
        /// <returns>Plex service instance.</returns>
        public static PlexMediaService GetDefaultInstance(string username, string password)
        {
            var plexConfig = new PlexMediaServerConfig
            {
                PlexAuthenticationRequestUser = new BasicAuth
                {
                    Username = username,
                    Password = password
                },
                BasicPlexHeaders = new PlexBasicRequestHeaders()
            };
            IHttpService httpService = new JsonHttpService(new HttpClient());
            IHeaderConstructor<PlexBasicRequestHeaders> plexBasicHeaderConstructorService = new PlexBasicHeaderConstructorService();
            IPlexAuthenticator authenticator = new PlexTvAuthenticator(
                httpService,
                plexBasicHeaderConstructorService,
                new BasicAuthHeaderConstructorService(), plexConfig);
            IPlexServerSettingsProvider settingsProvider = new PlexServerPreferencesProviderService(httpService, plexBasicHeaderConstructorService, plexConfig);

            return new PlexMediaService(plexConfig, httpService, authenticator, settingsProvider, plexBasicHeaderConstructorService);
        }
        
        /// <summary>
        /// Media server initialized to be called before consuming the service.
        /// The setting up of the configuration object should happen in this function.
        /// <param name="serverSelector">Delegate for selecting a server as the default upon discovery.</param>
        /// <param name="token">Cancellation token instance.</param>
        /// </summary>
        public async Task<InitializationStatus> InitializeAsync(Func<IEnumerable<Device>, string> serverSelector, CancellationToken token)
        {
            var authenticationResponseTask = _authenticator.AuthenticateAsync(token);
            var getServersResponseTask = _authenticator.GetAllServers(token);

            await Task.WhenAll(authenticationResponseTask, getServersResponseTask);
            
            if(authenticationResponseTask.Result?.User == null) return InitializationStatus.Unauthorised;
            if(getServersResponseTask.Result == null || !getServersResponseTask.Result.Any()) return InitializationStatus.NoServersDiscovered;
            
            Configuration.PlexAuthenticatedUser = authenticationResponseTask.Result.User;
            Configuration.DiscoveredServers = getServersResponseTask.Result;
            Configuration.ServerAddress = serverSelector(Configuration.DiscoveredServers);
            Configuration.ServerPreferences = await _serverSettingsProvider.GetServerSettingsAsync(token);

            if (Configuration.ServerPreferences?.Setting == null) return InitializationStatus.Error;

            return InitializationStatus.Ok;
        }
        
        public async Task<IEnumerable<Album>> GetAlbumsAsync(string libraryId, CancellationToken token)
        {
            var requestUrl = Endpoint.LibraryMusic.Description(Configuration.ServerAddress, libraryId.ThrowIfNullOrWhitespace(nameof(libraryId)), "9");
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            HttpRequest httpRequest = request
                .WithAuthToken(Configuration)
                .AcceptJson()
                .ToHttpRequest();
            HttpResponse<BasePlexResponse<MediaContainer>> response = await _httpService
                .RequestAsync<BasePlexResponse<MediaContainer>>(httpRequest, token);

            var albums = response
                .Response
                .MediaContainer
                .Metadata
                .Select(m =>
                {
                    return new Album
                    {
                        Id = m.Key,
                        Artist = m.ParentTitle,
                        Description = m.Summary,
                        Poster = $"{Configuration.ServerAddress}{m.Art}?{Configuration.QueryStringPlexToken}",
                        Thumbnail = $"{Configuration.ServerAddress}{m.Thumb}?{Configuration.QueryStringPlexToken}",
                        Title = m.Title,
                        SortingTitle = m.TitleSort,
                        Year = m.Year,
                        GetSongsAsync = (cancellationToken) => GetAlbumSongsAsync(m.Key, cancellationToken),
                        Collections = m.Collection.Select(x => x.Tag)
                    };
                })
                .ToList();

            return albums;
        }

        public async Task<IEnumerable<Song>> GetAlbumSongsAsync(string albumId, CancellationToken token)
        {
            var requestUrl = Endpoint.Children.Description(Configuration.ServerAddress, albumId.ThrowIfNullOrWhitespace(nameof(albumId)));
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            HttpRequest httpRequest = request
                .WithAuthToken(Configuration)
                .AcceptJson()
                .ToHttpRequest();
            HttpResponse<BasePlexResponse<MediaContainer>> response = await _httpService
                .RequestAsync<BasePlexResponse<MediaContainer>>(httpRequest, token);

            var songs = response
                .Response
                .MediaContainer
                .Metadata
                .Select(m =>
                {
                    Media media = m.Media.First();
                    var file = media.Part.First();
                    return new Song
                    {
                        Id = m.Key,
                        Title = m.Title,
                        SortingTitle = m.TitleSort,
                        AudioChannels = media.AudioChannels,
                        AudioCodec = media.AudioCodec,
                        Bitrate = media.Bitrate,
                        Size = file.Size,
                        Container = media.Container,
                        Description = m.Summary,
                        Duration = m.Duration,
                        StreamingUrl = file.Key,
                        Thumbnail = $"{Configuration.ServerAddress}{m.Thumb}?{Configuration.QueryStringPlexToken}",
                        FileName = System.IO.Path.GetFileName(file.File)
                    };
                })
                .ToList();

            return songs;
        }

        /// <summary>
        /// Get all movies async.
        /// </summary>
        /// <param name="libraryId">The ID of the library to get the content for.</param>
        /// <param name="token">Cancellation token instance.</param>
        /// <returns>Movies collection</returns>
        public async Task<IEnumerable<Movie>> GetMoviesAsync(string libraryId, CancellationToken token)
        {
            var requestUrl = Endpoint.LibraryMovies.Description(Configuration.ServerAddress, libraryId.ThrowIfNullOrWhitespace(nameof(libraryId)));
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            HttpRequest httpRequest = request
                .WithAuthToken(Configuration)
                .AcceptJson()
                .ToHttpRequest();
            HttpResponse<BasePlexResponse<MediaContainer>> response = await _httpService
                .RequestAsync<BasePlexResponse<MediaContainer>>(httpRequest, token);

            var movies = response
                .Response
                .MediaContainer
                .Metadata
                .Select(m =>
                {
                    Media media = m.Media.First();

                    return new Movie
                    {
                        Id = m.Key,
                        AudioChannels = media.AudioChannels,
                        AudioCodec = media.AudioCodec,
                        Bitrate = media.Bitrate,
                        Container = media.Container,
                        Description = m.Summary,
                        Duration = m.Duration,
                        Height = media.Height,
                        Width = media.Width,
                        Poster = $"{Configuration.ServerAddress}{m.Art}?{Configuration.QueryStringPlexToken}",
                        Rating = m.Rating,
                        StreamingUrl = $"{Configuration.ServerAddress}{media.Part.First().Key}?{Configuration.QueryStringPlexToken}",
                        Studio = m.Studio,
                        Thumbnail = $"{Configuration.ServerAddress}{m.Thumb}?{Configuration.QueryStringPlexToken}",
                        Title = m.Title,
                        SortingTitle = m.TitleSort,
                        VideoCodec = media.VideoCodec,
                        ViewCount = m.ViewCount,
                        Year = m.Year,
                        Collections = m.Collection.Select(x => x.Tag)
                    };
                })
                .ToList();

            return movies;
        }

        /// <summary>
        /// Get all collections async.
        /// </summary>
        /// <param name="libraryId">The ID of the library to get the content for.</param>
        /// <param name="token">Cancellation token instance.</param>
        /// <returns>Movies collection</returns>
        public async Task<IEnumerable<Collection>> GetCollectionsAsync(string libraryId, CancellationToken token)
        {
            var requestUrl = Endpoint.LibraryCollections.Description(Configuration.ServerAddress, libraryId.ThrowIfNullOrWhitespace(nameof(libraryId)));
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            HttpRequest httpRequest = request
                .WithAuthToken(Configuration)
                .AcceptJson()
                .ToHttpRequest();
            HttpResponse<BasePlexResponse<MediaContainer>> response = await _httpService
                .RequestAsync<BasePlexResponse<MediaContainer>>(httpRequest, token);

            var collections = response
                .Response
                .MediaContainer
                .Metadata
                .Select(m =>
                {
                    return new Collection
                    {
                        Id = m.Key,
                        Description = m.Summary,
                        Thumbnail = $"{Configuration.ServerAddress}{m.Thumb}?{Configuration.QueryStringPlexToken}",
                        Title = m.Title,
                        SortingTitle = m.TitleSort,
                    };
                })
                .ToList();

            return collections;
        }

        /// <summary>
        /// Get all collections async.
        /// </summary>
        /// <param name="libraryId">The ID of the library to get the content for.</param>
        /// <param name="token">Cancellation token instance.</param>
        /// <returns>Movies collection</returns>
        public async Task<IEnumerable<Collection>> GetCollectionsSimpleAsync(string libraryId, CancellationToken token)
        {
            var requestUrl = Endpoint.LibraryMusicCollections.Description(Configuration.ServerAddress, libraryId.ThrowIfNullOrWhitespace(nameof(libraryId)));
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            HttpRequest httpRequest = request
                .WithAuthToken(Configuration)
                .AcceptJson()
                .ToHttpRequest();
            HttpResponse<BasePlexResponse<MediaContainer>> response = await _httpService
                .RequestAsync<BasePlexResponse<MediaContainer>>(httpRequest, token);

            var collections = response
                .Response
                .MediaContainer
                .Directory
                .Select(m =>
                {
                    return new Collection
                    {
                        Id = m.Key,
                        Title = m.Title
                    };
                })
                .ToList();

            return collections;
        }

        /// <summary>
        /// Collection of libraries from the server
        /// <param name="token">Cancellation token instance.</param>
        /// </summary>
        public async Task<IEnumerable<ILibrary>> GetAllLibrariesAsync(CancellationToken token)
        {
            var requestUrl = Endpoint.Libraries.Description(Configuration.ServerAddress);
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            HttpRequest httpRequest = request
                .WithAuthToken(Configuration)
                .AddRequestHeaders(_plexBasicHeaders)
                .AcceptJson()
                .ToHttpRequest();
            HttpResponse<BasePlexResponse<Libraries>> response = await _httpService
                .RequestAsync<BasePlexResponse<Libraries>>(httpRequest, token);
            List<ILibrary> result = response
                .Response
                .MediaContainer
                .Directory
                .Select(d => GetLibrary(d, token))
                .ToList();

            return result;
        }

        /// <summary>
        /// Convert the string of library type to an enum value.
        /// </summary>
        /// <param name="str">String library type.</param>
        /// <returns>Enum library type.</returns>
        private LibraryType GetTypeFromString(string str)
        {
            switch (str)
            {
                case "movie":
                    return LibraryType.Movie;
                case "show":
                    return LibraryType.TvSeries;
                case "artist":
                    return LibraryType.Music;
                default:
                    return LibraryType.Other;
            }
        }

        private ILibrary GetLibrary(Directory dir, CancellationToken token)
        {
            var type = GetTypeFromString(dir.Type);
            ILibrary library;
            switch (type)
            {
                case LibraryType.Movie:
                    library = new MovieLibrary
                    {
                        GetMoviesAsync = (cancellationToken) => GetMoviesAsync(dir.Key, cancellationToken),
                        GetCollectionsAsync = (cancellationToken) => GetCollectionsAsync(dir.Key, cancellationToken)
                    };
                    break;
                case LibraryType.Music:
                    library = new MusicLibrary
                    {
                        GetAlbumsAsync = (cancellationToken) => GetAlbumsAsync(dir.Key, cancellationToken),
                        GetCollectionsAsync = (cancellationToken) => GetCollectionsSimpleAsync(dir.Key, cancellationToken)
                    };
                    break;
                default:
                    library = new OtherLibrary
                    {
                        GetCollectionsAsync = (cancellationToken) => GetCollectionsAsync(dir.Key, cancellationToken)
                    };
                    break;
            }
            library.Id = dir.Key;
            library.Poster = $"{Configuration.ServerAddress}{dir.Art}?{Configuration.QueryStringPlexToken}";
            library.Thumbnail = $"{Configuration.ServerAddress}{dir.Thumb}?{Configuration.QueryStringPlexToken}";
            library.Title = dir.Title;
            return library;
        }
    }
}