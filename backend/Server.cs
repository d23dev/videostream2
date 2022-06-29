using System.Net;
using System.Text;
using System.Text.Json;

namespace VideoStreamBackend
{
	public struct FrameData
	{
		public long UnixTime { get; set; }
		public string Colors { get; set; }
		public int SizeX { get; set; }
		public int SizeY { get; set; }
	}
	public struct VideoStateResponse
	{
		public FrameData[] FrameData { get; set; }
		public string VideoPath { get; set; }
		public float CurrentVideoTime { get; set; }
		public float TotalVideoLength { get; set; }
		public string CurrentCaption { get; set; }
		public long UnixTime { get; set; }
	}

	public class Server
	{
		public HttpListener listener;
		public MediaPlayerWrapper player;
		public string url = "http://localhost:8000/";
		
		public Server (MediaPlayerWrapper mediaPlayerWrapper)
		{
			player = mediaPlayerWrapper;

			listener = new HttpListener ();
			listener.Prefixes.Add(url);
			listener.Start();
			Console.WriteLine("Listening on " + url);

			HandleIncomingConnections();
		}

		public string HandleGet (HttpListenerRequest req, HttpListenerResponse res)
		{
			if (req.Url == null) return "URI_NULL";

			switch (req.Url.LocalPath)
			{
				case "/videostate":
					return JsonSerializer.Serialize(new VideoStateResponse
					{
						FrameData = player.FrameDataQueue.ToArray(),
						VideoPath = player.GetMrl(),
						CurrentVideoTime = player.GetTimePosition(),
						TotalVideoLength = player.GetLength(),
						CurrentCaption = "",
						UnixTime = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
					});
				case "/playeraction":
					var action = req.QueryString.Get("action");
					var time = req.QueryString.Get("time");
					var path = req.QueryString.Get("path");
					switch (action)
					{
						default: return "INVALID_ACTION";
						case "Resume":
							player.Resume();
							break;
						case "Pause":
							player.Pause();
							break;
						case "Stop":
							player.Stop();
							break;
						case "Seek":
							if (time == null) return "NO_TIME_GIVEN";

							player.Seek(Convert.ToInt32(time));
							break;
						case "Load":
							if (path == null) return "NO_PATH_GIVEN";

							if (path.Contains("http"))
							{
								player.LoadUri(path);
							}
							else
							{
								player.Load(path);
							}
							break;

					}
					return "SUCCESS";
			}
			return "None";
		}

		public string HandlePost(HttpListenerRequest req, HttpListenerResponse res)
		{
			if (req.Url == null) return "URI_NULL";

			return "None";
		}

		public async Task HandleIncomingConnections()
		{
			var running = true;

			while (running)
			{
				var ctx = await listener.GetContextAsync();

				var req = ctx.Request;
				var res = ctx.Response;

				string result;
				if (req.HttpMethod == "GET")
				{
					result = HandleGet(req, res);
				} 
				else if (req.HttpMethod == "POST") 
				{
					result = HandlePost(req, res);
				} 
				else 
				{
					result = "None";
				}

				byte[] data = Encoding.UTF8.GetBytes(result);
				res.ContentType = "text/plain";
				res.ContentEncoding = Encoding.UTF8;
				res.ContentLength64 = data.LongLength;

				// Write out to the response stream (asynchronously), then close it
				await res.OutputStream.WriteAsync(data, 0, data.Length);
				res.Close();
			}
		}
	}
}