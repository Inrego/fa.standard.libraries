﻿using System;
using System.Collections.Generic;
using System.Text;

namespace FrostAura.Libraries.MediaServer.Core.Models.Content
{
    public class Song
    {
        /// <summary>
        /// Song id.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Song title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Song file name.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Song file size.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Song sorting title.
        /// </summary>
        public string SortingTitle { get; set; }

        /// <summary>
        /// Song description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Full thumbnail URL.
        /// </summary>
        public string Thumbnail { get; set; }

        /// <summary>
        /// Duration in milliseconds.
        /// </summary>
        public int Duration { get; set; }

        /// <summary>
        /// Song bitrate.
        /// </summary>
        public int Bitrate { get; set; }

        /// <summary>
        /// Count of audio channels.
        /// </summary>
        public int AudioChannels { get; set; }

        /// <summary>
        /// Audio codec.
        /// </summary>
        public string AudioCodec { get; set; }

        /// <summary>
        /// Video streaming url.
        /// </summary>
        public string StreamingUrl { get; set; }

        /// <summary>
        /// Media file container.
        /// </summary>
        public string Container { get; set; }
    }
}
