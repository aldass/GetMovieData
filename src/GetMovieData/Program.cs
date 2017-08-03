using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using Newtonsoft.Json;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;

namespace GetMovieData
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (string.IsNullOrEmpty(args[0]))
                throw new ArgumentException("Invalid param API Key for https://api.themoviedb.org");
            var paramApiKey = args[0];
            HttpClient http = new HttpClient();
            // 10049
            var movieIds = new List<int> { 464603, 463910, 461714, 446539, 446298, 423627, 416149, 412678, 411632, 404654, 397003, 394223, 389425, 364433, 332976, 313520, 313519, 313515, 309366, 306325, 298667, 297702, 286668, 286465, 285096, 276615, 273817, 272099, 272061, 257298, 227359, 201724, 168454, 142463, 118683, 118483, 68426, 47881, 47336, 46103, 36272, 31002, 30806, 28465, 26583, 24546, 24351, 23631, 18816, 18472, 18248, 18070, 17317, 17314, 15039, 14771, 14452, 14362, 14289, 14180, 13722, 12542, 10877, 10173, 10167, 10049, 9914, 9625, 9624, 9569, 9395, 8845, 8382, 6058, 3512, 2320 };
            var baseUrl = "https://api.themoviedb.org/3/movie/";

            var apiKey = $"api_key={paramApiKey}";


            foreach (int movieId in movieIds)
            {
                dynamic movie = null;
                List<dynamic> casts = new List<dynamic>();
                List<dynamic> crews = new List<dynamic>();
                string mpaa = "R";

                // movie
                var movieUrl = $"{baseUrl}{movieId}?{apiKey}";
                Console.WriteLine(movieUrl);
                var rsp1 = http.GetAsync(movieUrl).Result;
                if (rsp1.IsSuccessStatusCode)
                {
                    var result = rsp1.Content.ReadAsStringAsync().Result;
                    movie = JsonConvert.DeserializeObject<dynamic>(result);
                    Console.WriteLine(movie.id.Value);
                }

                var castUrl = $"{baseUrl}{movieId}/credits?{apiKey}";
                Console.WriteLine(castUrl);
                var rsp2 = http.GetAsync(castUrl).Result;
                if (rsp2.IsSuccessStatusCode)
                {
                    var result = rsp2.Content.ReadAsStringAsync().Result;
                    var credits = JsonConvert.DeserializeObject<dynamic>(result);
                    if (credits?.cast != null) foreach (dynamic cast in credits?.cast) casts.Add(cast);
                    if (credits?.crew != null) foreach (dynamic crew in credits?.crew) crews.Add(crew);
                    Console.WriteLine(credits.id.Value);
                }

                var relDtUrl = $"{baseUrl}{movieId}/release_dates?{apiKey}";
                Console.WriteLine(relDtUrl);
                var rsp3 = http.GetAsync(relDtUrl).Result;
                if (rsp3.IsSuccessStatusCode)
                {
                    var result = rsp3.Content.ReadAsStringAsync().Result;
                    var relDts = JsonConvert.DeserializeObject<dynamic>(result);
                    foreach (var rd in relDts.results) if (rd.iso_3166_1.Value == "US") mpaa = rd.release_dates[0].certification.Value ?? "";
                    Console.WriteLine(relDts.id.Value);
                }

                // insert into the db;
                dbInsert(movie, casts, crews, mpaa);

                // super lazy way of making sure we don't go over the 4 req/sec rate limit
                Thread.Sleep(999);
            }

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }

        static void dbInsert(dynamic jsonMovie, List<dynamic> jsonCasts, List<dynamic> jsonCrews, string mpaa)
        {
            var location = System.Reflection.Assembly.GetEntryAssembly().Location;
            var directory = System.IO.Path.GetDirectoryName(location);
            var dbFile = directory + "\\..\\..\\..\\allseagal.db";

            using (var connection = new SqliteConnection("" +
                                                         new SqliteConnectionStringBuilder
                                                         {
                                                             DataSource = dbFile
                                                         }))
            {
                connection.Open();

                using (var tx = connection.BeginTransaction())
                {
                    var sqlCmdMovies = connection.CreateCommand();
                    sqlCmdMovies.Transaction = tx;
                    sqlCmdMovies.CommandText = "INSERT OR REPLACE INTO ";
                    sqlCmdMovies.CommandText += "movie( [id], [imdbId], [title], [year], [estimatedBudget], [boxOfficeGross], [stars], [rating], [runningTime], [description], [blurb], [coverImage]) ";
                    sqlCmdMovies.CommandText += "VALUES( $id, $imdbId, $title, $year, $estimatedBudget, $boxOfficeGross, $stars, $rating, $runningTime, $description, $blurb, $coverImage);";

                    DateTime dts = Convert.ToDateTime(!string.IsNullOrEmpty(jsonMovie.release_date?.Value) ? jsonMovie.release_date.Value : "1900-01-01");
                    string blurb = !string.IsNullOrEmpty(jsonMovie.tagLine?.Value) ? jsonMovie.tagLine.Value : "The man. The myth. The Legend. Steven Seagal in..." + jsonMovie.title?.Value ?? "N/A";
                    string coverImage = jsonMovie.poster_path?.Value != null ? "//image.tmdb.org/t/p/w300_and_h450_bestv2" + jsonMovie.poster_path.Value : "";

                    sqlCmdMovies.Parameters.AddWithValue("$id", jsonMovie.id?.Value);
                    sqlCmdMovies.Parameters.AddWithValue("$imdbId", jsonMovie.imdb_id?.Value ?? "");
                    sqlCmdMovies.Parameters.AddWithValue("$title", jsonMovie.title?.Value ?? "N/A");
                    sqlCmdMovies.Parameters.AddWithValue("$year", dts.Year);
                    sqlCmdMovies.Parameters.AddWithValue("$estimatedBudget", jsonMovie.budget?.Value);
                    sqlCmdMovies.Parameters.AddWithValue("$boxOfficeGross", jsonMovie.revenue?.Value);
                    sqlCmdMovies.Parameters.AddWithValue("$stars", jsonMovie.vote_average?.Value);
                    sqlCmdMovies.Parameters.AddWithValue("$rating", !string.IsNullOrEmpty(mpaa) ? mpaa : "NR");
                    sqlCmdMovies.Parameters.AddWithValue("$runningTime", jsonMovie.runtime?.Value ?? 0);
                    sqlCmdMovies.Parameters.AddWithValue("$description", jsonMovie.overview?.Value ?? "N/A");
                    sqlCmdMovies.Parameters.AddWithValue("$blurb", blurb);
                    sqlCmdMovies.Parameters.AddWithValue("$coverImage", coverImage);

                    sqlCmdMovies.ExecuteNonQuery();

                    foreach (dynamic jsonCast in jsonCasts)
                    {
                        var sqlCmdCast = connection.CreateCommand();
                        sqlCmdCast.Transaction = tx;
                        sqlCmdCast.CommandText = "INSERT OR REPLACE INTO ";
                        sqlCmdCast.CommandText += "[cast]( [name], [role], [img], [gender], [order], [fkMovieId], [isCrew]) ";
                        sqlCmdCast.CommandText += "VALUES( $name, $role, $img, $gender, $order, $fkMovieId, 0);";

                        string img = jsonCast.profile_path?.Value != null ? "//image.tmdb.org/t/p/w138_and_h175_bestv2" + jsonCast.profile_path.Value : "";

                        sqlCmdCast.Parameters.AddWithValue("$fkMovieId", jsonMovie.id?.Value);
                        sqlCmdCast.Parameters.AddWithValue("$name", jsonCast.name?.Value ?? "");
                        sqlCmdCast.Parameters.AddWithValue("$role", jsonCast.character?.Value ?? "");
                        sqlCmdCast.Parameters.AddWithValue("$img", img ?? "");
                        sqlCmdCast.Parameters.AddWithValue("$gender", jsonCast.gender?.Value  != null ?  (jsonCast.gender.Value == 2 ? "M" : "F") : "");
                        sqlCmdCast.Parameters.AddWithValue("$order", jsonCast.order?.Value ?? 4096);
                        sqlCmdCast.ExecuteNonQuery();
                    }

                    foreach (dynamic jsonCrew in jsonCrews)
                    {
                        var sqlCmdCrew = connection.CreateCommand();
                        sqlCmdCrew.Transaction = tx;
                        sqlCmdCrew.CommandText = "INSERT OR REPLACE INTO ";
                        sqlCmdCrew.CommandText += "[cast]( [fkMovieId], [name], [role], [img], [gender], [order], [isCrew]) ";
                        sqlCmdCrew.CommandText += "VALUES( $fkMovieId, $name, $role, $img, $gender, 4096, 1);";

                        string img = jsonCrew.profile_path?.Value != null ? "//image.tmdb.org/t/p/w138_and_h175_bestv2" + jsonCrew.profile_path.Value : "";

                        sqlCmdCrew.Parameters.AddWithValue("$fkMovieId", jsonMovie.id?.Value);
                        sqlCmdCrew.Parameters.AddWithValue("$name", jsonCrew.name?.Value ?? "");
                        sqlCmdCrew.Parameters.AddWithValue("$role", jsonCrew.job?.Value ?? "");
                        sqlCmdCrew.Parameters.AddWithValue("$img", img ?? "");
                        sqlCmdCrew.Parameters.AddWithValue("$gender", jsonCrew.gender?.Value != null ? (jsonCrew.gender.Value == 2 ? "M" : "F") : "U");
                        sqlCmdCrew.ExecuteNonQuery();
                    }

                    // commit the tx
                    tx.Commit();
                }


                var chkCmd = connection.CreateCommand();
                chkCmd.CommandText = "SELECT * FROM movie where id = " + jsonMovie.id.Value;
                using (var reader = chkCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var title = reader.GetString(reader.GetOrdinal("title"));
                        Console.WriteLine(title);
                    }
                }

            }
        }

        static string getRoleUser(string role, List<dynamic> jsonCrews)
        {
            foreach (dynamic crew in jsonCrews)
            {
                if (!string.IsNullOrEmpty(crew.job?.Value) && crew.job?.Value.ToString().StartsWith(role))
                    return crew.name?.Value?.ToString() ?? "N/A";
            }
            return "N/A";
        }
    }
}
