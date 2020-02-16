using FrostAura.Libraries.MediaServer.Core.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FrostAura.Libraries.MediaServer.Core.Models.Content
{
    /// <summary>
    /// Generic library model.
    /// </summary>
    [DebuggerDisplay("{Title} - {Type}")]
    public class MusicLibrary : ILibrary
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Thumbnail { get; set; }
        public string Poster { get; set; }
        public LibraryType Type { get; set; } = LibraryType.Music;

        public Func<CancellationToken, Task<IEnumerable<Album>>> GetAlbumsAsync;
    }
}
