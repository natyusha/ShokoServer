﻿
namespace Shoko.Models.Server
{
    public class AniDB_Recommendation
    {
        public int AniDB_RecommendationID { get; set; }
        public int AnimeID { get; set; }
        public int UserID { get; set; }
        public int RecommendationType { get; set; }
        public string RecommendationText { get; set; }
    }
}