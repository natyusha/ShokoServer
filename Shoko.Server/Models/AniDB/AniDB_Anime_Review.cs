﻿namespace Shoko.Models.Server
{
    public class AniDB_Anime_Review
    {
        #region Server DB columns

        public int AniDB_Anime_ReviewID { get; set; }
        public int AnimeID { get; set; }
        public int ReviewID { get; set; }

        #endregion
    }
}