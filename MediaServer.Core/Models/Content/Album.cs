using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FrostAura.Libraries.MediaServer.Core.Models.Content
{
    public class Album
    {
        /// <summary>
        /// Album title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Album artist.
        /// </summary>
        public string Artist { get; set; }
        
        /// <summary>
        /// Album description.
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Year of release.
        /// </summary>
        public int Year { get; set; }
        
        /// <summary>
        /// Full thumbnail URL.
        /// </summary>
        public string Thumbnail { get; set; }

        /// <summary>
        /// Movie poster full URL.
        /// </summary>
        public string Poster { get; set; }

        public Func<CancellationToken, Task<IEnumerable<Song>>> GetSongsAsync;
    }
}
