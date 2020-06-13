using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace FrostAura.Libraries.MediaServer.Plex.Models.Content
{
    /// <summary>
    /// Simple collection.
    /// </summary>
    [DebuggerDisplay("{Title}")]
    public class CollectionDirectory
    {
        public string FastKey { get; set; }
        public string Key { get; set; }
        public string Title { get; set; }
    }
}
