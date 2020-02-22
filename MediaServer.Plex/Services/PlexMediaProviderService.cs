using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FrostAura.Libraries.Core.Extensions.Decoration;
using FrostAura.Libraries.Core.Extensions.Validation;
using FrostAura.Libraries.Http.Extensions;
using FrostAura.Libraries.Http.Interfaces;
using FrostAura.Libraries.Http.Models.Requests;
using FrostAura.Libraries.Http.Models.Responses;
using FrostAura.Libraries.MediaServer.Core.Enums;
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
    /// Provider for plex libraries.
    /// </summary>
    public class PlexMediaProviderService : IPlexMediaProvider
    {
        /// <summary>
        /// Instance of static http service to use in making web requests.
        /// </summary>
        private IHttpService _httpService { get; }
        
        /// <summary>
        /// Constructor for Plex headers.
        /// </summary>
        private IDictionary<string, string> _plexBasicHeaders { get; }
        
        /// <summary>
        /// Media server specific configuration. Should be set in a constructor.
        /// </summary>
        private PlexMediaServerConfig _configuration { get; }
        
        /// <summary>
        /// Overloaded constructor to pass configuration.
        /// </summary>
        /// <param name="httpService">Instance of static http service to use in making web requests.</param>
        /// <param name="plexBasicHeadersConstructor">Constructor for Plex headers.</param>
        /// <param name="configuration">Media server specific configuration.</param>
        public PlexMediaProviderService(IHttpService httpService,
            IHeaderConstructor<PlexBasicRequestHeaders> plexBasicHeadersConstructor,
            PlexMediaServerConfig configuration)
        {
            _configuration = configuration
                .ThrowIfNull(nameof(configuration))
                .ThrowIfInvalid(nameof(configuration));
            
            _httpService = httpService
                .ThrowIfNull(nameof(httpService));
            _plexBasicHeaders = plexBasicHeadersConstructor
                .ThrowIfNull(nameof(plexBasicHeadersConstructor))
                .ConstructRequestHeaders(configuration.BasicPlexHeaders);
        }
        
        /// <summary>
        /// Collection of libraries from the server
        /// <param name="token">Cancellation token instance.</param>
        /// </summary>
        public async Task<IEnumerable<ILibrary>> GetAllLibrariesAsync(CancellationToken token)
        {
            var requestUrl = Endpoint.Libraries.Description(_configuration.ServerAddress);
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            HttpRequest httpRequest = request
                .WithAuthToken(_configuration)
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
            switch(type)
            {
                case LibraryType.Movie:
                    library = new MovieLibrary
                    {
                        GetMoviesAsync = (cancellationToken) => GetMoviesAsync(dir.Key, dir.Type, cancellationToken)
                    };
                    break;
                case LibraryType.Music:
                    library = new MusicLibrary
                    {
                        GetAlbumsAsync = (cancellationToken) => GetAlbumsAsync(dir.Key, dir.Type, cancellationToken)
                    };
                    break;
                default:
                    library = new OtherLibrary();
                    break;
            }
            library.Id = dir.Key;
            library.Poster = $"{_configuration.ServerAddress}{dir.Art}?{_configuration.QueryStringPlexToken}";
            library.Thumbnail = $"{_configuration.ServerAddress}{dir.Thumb}?{_configuration.QueryStringPlexToken}";
            library.Title = dir.Title;
            return library;
        }
        
        /// <summary>
        /// Get all movies async.
        /// </summary>
        /// <param name="libraryId">The ID of the library to get the content for.</param>
        /// <param name="libraryType">The string type for the library.</param>
        /// <param name="token">Cancellation token instance.</param>
        /// <returns>Movies collection</returns>
        private async Task<IEnumerable<Movie>> GetMoviesAsync(string libraryId, string libraryType, CancellationToken token)
        {
            LibraryType type = GetTypeFromString(libraryType.ThrowIfNullOrWhitespace(nameof(libraryType)));
            var movies = new List<Movie>();

            if (type != LibraryType.Movie) return movies;
            
            var requestUrl = Endpoint.LibraryMovies.Description(_configuration.ServerAddress, libraryId.ThrowIfNullOrWhitespace(nameof(libraryId)));
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            HttpRequest httpRequest = request
                .WithAuthToken(_configuration)
                .AcceptJson()
                .ToHttpRequest();
            HttpResponse<BasePlexResponse<MediaContainer>> response = await _httpService
                .RequestAsync<BasePlexResponse<MediaContainer>>(httpRequest, token);

            movies = response
                .Response
                .MediaContainer
                .Metadata
                .Select(m =>
                {
                    Media media = m.Media.First();
                    
                    return new Movie
                    {
                        AudioChannels = media.AudioChannels,
                        AudioCodec = media.AudioCodec,
                        Bitrate = media.Bitrate,
                        Container = media.Container,
                        Description = m.Summary,
                        Duration = m.Duration,
                        Height = media.Height,
                        Width = media.Width,
                        Poster = $"{_configuration.ServerAddress}{m.Art}?{_configuration.QueryStringPlexToken}",
                        Rating = m.Rating,
                        StreamingUrl = $"{_configuration.ServerAddress}{media.Part.First().Key}?{_configuration.QueryStringPlexToken}",
                        Studio = m.Studio,
                        Thumbnail = $"{_configuration.ServerAddress}{m.Thumb}?{_configuration.QueryStringPlexToken}",
                        Title = m.Title,
                        VideoCodec = media.VideoCodec,
                        ViewCount = m.ViewCount,
                        Year = m.Year
                    };
                })
                .ToList();

            return movies;
        }
        private async Task<IEnumerable<Album>> GetAlbumsAsync(string libraryId, string libraryType, CancellationToken token)
        {
            LibraryType type = GetTypeFromString(libraryType.ThrowIfNullOrWhitespace(nameof(libraryType)));
            var albums = new List<Album>();

            if (type != LibraryType.Music) return albums;

            var requestUrl = Endpoint.LibraryMusic.Description(_configuration.ServerAddress, libraryId.ThrowIfNullOrWhitespace(nameof(libraryId)), "9");
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            HttpRequest httpRequest = request
                .WithAuthToken(_configuration)
                .AcceptJson()
                .ToHttpRequest();
            HttpResponse<BasePlexResponse<MediaContainer>> response = await _httpService
                .RequestAsync<BasePlexResponse<MediaContainer>>(httpRequest, token);
            var respStr = await response.ResponseMessage.Content.ReadAsStringAsync();
            albums = response
                .Response
                .MediaContainer
                .Metadata
                .Select(m =>
                {
                    Media media = m.Media.First();

                    return new Album
                    {
                        
                    };
                })
                .ToList();

            return albums;
        }
    }
}