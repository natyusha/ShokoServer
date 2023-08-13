using System;
using Shoko.Models.Enums;

namespace Shoko.Models.Server
{
    public class CrossRef_AniDB_MAL : ICloneable
    {
        public int Id { get; set; }

        public int AnidbAnimeId { get; set; }

        public int MalAnimeId { get; set; }

        public CrossRefSource Source { get; set; }

        public object Clone()
        {
            return new CrossRef_AniDB_MAL
            {
                Id = Id,
                AnidbAnimeId = AnidbAnimeId,
                MalAnimeId = MalAnimeId,
                Source = Source,
            };
        }
    }
}
