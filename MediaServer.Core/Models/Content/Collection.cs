using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace FrostAura.Libraries.MediaServer.Core.Models.Content
{
    /// <summary>
    /// Movie container.
    /// </summary>
    [DebuggerDisplay("{Title}")]
    public class Collection
    {
        /// <summary>
        /// Collection id.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Collection title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Collection sorting title.
        /// </summary>
        public string SortingTitle { get; set; }

        /// <summary>
        /// Collection description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Full thumbnail URL.
        /// </summary>
        public string Thumbnail { get; set; }
    }
}
