using System;
using System.Collections.Generic;
using NLog;
using Shoko.Models.Client;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;

namespace Shoko.Server.Providers.TMDB;

public class TMDB_Movie_Result
{
    private static Logger logger = LogManager.GetCurrentClassLogger();

    public int MovieID { get; set; }
    public string MovieName { get; set; }
    public string OriginalName { get; set; }
    public string Overview { get; set; }
    public double Rating { get; set; }

    public List<TMDB_Image_Result> Images { get; set; }

    public override string ToString()
    {
        return "TMDB_Movie_Result: " + MovieID + ": " + MovieName;
    }

    public bool Populate(Movie movie, ImagesWithId imgs)
    {
        try
        {
            Images = new List<TMDB_Image_Result>();

            MovieID = movie.Id;
            MovieName = movie.Title;
            OriginalName = movie.Title;
            Overview = movie.Overview;
            Rating = movie.VoteAverage;

            if (imgs != null && imgs.Backdrops != null)
            {
                foreach (var img in imgs.Backdrops)
                {
                    var imageResult = new TMDB_Image_Result();
                    if (imageResult.Populate(img, "backdrop"))
                    {
                        Images.Add(imageResult);
                    }
                }
            }

            if (imgs != null && imgs.Posters != null)
            {
                foreach (var img in imgs.Posters)
                {
                    var imageResult = new TMDB_Image_Result();
                    if (imageResult.Populate(img, "poster"))
                    {
                        Images.Add(imageResult);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return false;
        }

        return true;
    }

    public CL_MovieDBMovieSearch_Response ToContract()
    {
        var cl = new CL_MovieDBMovieSearch_Response
        {
            MovieID = MovieID, MovieName = MovieName, OriginalName = OriginalName, Overview = Overview
        };
        return cl;
    }
}
