using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FrostAura.Libraries.MediaServer.Core.Enums;

namespace FrostAura.Libraries.MediaServer.Core.Models.Content
{
        public interface ILibrary
    {
        /// <summary>
        /// Id of the library.
        /// </summary>
        string Id { get; set; }
        
        /// <summary>
        /// The short description of the library.
        /// </summary>
        string Title { get; set; }

        /// <summary>
        /// The library thumbnail full URL.
        /// </summary>
        string Thumbnail { get; set; }

        /// <summary>
        /// The library poster full URL.
        /// </summary>
        string Poster { get; set; }

        /// <summary>
        /// Type of library.
        /// </summary>
        LibraryType Type { get; set; }
    }
}