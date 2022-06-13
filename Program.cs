using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace YoutubeDownloader;

public class Program
{
    private static readonly YoutubeClient client = new();

    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] == "/?")
        {
            Console.WriteLine("{0} <url(s)> [diretório]", Path.GetFileName(Environment.GetCommandLineArgs()[0]));
            return 0;
        }

        string destDirname = Environment.CurrentDirectory;
        List<Video> videos = new();
        List<Playlist> playlists = new();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (VideoId.TryParse(arg) != null) videos.Add(client.Videos.GetAsync(arg).Result);
            else if (PlaylistId.TryParse(arg) != null) playlists.Add(client.Playlists.GetAsync(arg).Result);
            else if (i + 1 == args.Length)
            {
                try
                {
                    DirectoryInfo destDirInfo = new(args[i]);
                    if (!destDirInfo.Exists) destDirInfo.Create();
                    destDirname = destDirInfo.FullName;
                }
                catch { }
            }
        }

        DownloadVideosAsync(videos, playlists, destDirname).GetAwaiter().GetResult();

        return 0;
    }

    private static async Task DownloadVideosAsync(List<Video> videos, List<Playlist> playlists, string destDirname)
    {
        foreach (Video video in videos)
        {
            Console.Write($"{video.Title} ... ");

            IVideoStreamInfo streamInfo = (await client.Videos.Streams.GetManifestAsync(video.Id)).GetMuxedStreams().GetWithHighestVideoQuality();
            await client.Videos.Streams.DownloadAsync(streamInfo, $"{destDirname}\\{WithoutInvalidChars(video.Title, false)}.mp4");

            Console.WriteLine("100%");
        }

        foreach (Playlist playlist in playlists)
        {
            Console.WriteLine($"[Playlist]\t{playlist.Title}");

            PlaylistVideo[] pVideos = (await client.Playlists.GetVideosAsync(playlist.Id)).ToArray();

            for (int i = 0; i < pVideos.Length; i++)
            {
                PlaylistVideo video = pVideos[i];

                Console.Write($"\t\t[{i + 1}/{pVideos.Length}] {video.Title} ... ");

                string dirname = WithoutInvalidChars(playlist.Title, true);
                if (!Directory.Exists(dirname)) new DirectoryInfo(destDirname).CreateSubdirectory(dirname);
                string filename = $"{destDirname}\\{dirname}\\{WithoutInvalidChars(video.Title, false)}";

                try
                {
                    IVideoStreamInfo streamInfo = (await client.Videos.Streams.GetManifestAsync(video.Id)).GetMuxedStreams().GetWithHighestVideoQuality();
                    await client.Videos.Streams.DownloadAsync(streamInfo, $"{filename}.mp4");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    File.WriteAllText($"{filename}.txt", e.Message);
                }

                Console.WriteLine("100%");
            }
        }
    }

    private static string WithoutInvalidChars(string name, bool isDir)
    {
        char[] invalidChars = isDir ? Path.GetInvalidPathChars() : Path.GetInvalidFileNameChars();
        return new(name.Where(c => !invalidChars.Contains(c)).ToArray());
    }
}