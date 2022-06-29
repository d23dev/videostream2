using LibVLCSharp.Shared;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VideoStreamBackend
{
	public class MediaPlayerWrapper
	{
		private static string CharSet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

		private const uint Width = 256;
		private const uint Height = 144;

		private const uint BytePerPixel = 3;

		private readonly uint Pitch;
		private readonly uint Lines;

		private MemoryMappedFile? CurrentFile;

		private LibVLC libvlc;
		private MediaPlayer player;

		public FixedSizedQueue<FrameData> FrameDataQueue = new();

		private IntPtr Lock (IntPtr opaque, IntPtr planes)
		{
			// create memory file and tell LibVLC to write the image data to it
			// god, i hate pointers
			var mappedFile = MemoryMappedFile.CreateNew(null, Pitch * Lines);
			var accessor = mappedFile.CreateViewAccessor();
			Marshal.WriteIntPtr(planes, accessor.SafeMemoryMappedViewHandle.DangerousGetHandle());

			CurrentFile = mappedFile;

			return IntPtr.Zero;
		}

		private void Display (IntPtr opaque, IntPtr pattern)
		{
			if (CurrentFile == null) return;

			// vlc has written the image data to CurrentFile
			var stream = CurrentFile.CreateViewStream();
			var length = Width * Height * BytePerPixel;
			var chars = new byte[length];
			var charsIndex = 0;
			for (var i = 0; i < stream.Length; i++)
			{
				var nextByte = stream.ReadByte();
				// if in an empty byte zone, skip
				var byteX = i % Pitch;
				if (byteX > Width * BytePerPixel - 1) continue;
				uint y = (uint)(i / Pitch);
				if (y > Height - 1) break;

				chars[charsIndex] = (byte)CharSet[nextByte / 4];
				charsIndex++;
			}

			FrameDataQueue.Enqueue(new FrameData
				{
					Colors = Encoding.Default.GetString(chars),
					UnixTime = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
					SizeX = (int)Width,
					SizeY = (int)Height,
				}
			);

			CurrentFile.Dispose();
			CurrentFile = null;
		}

		public MediaPlayerWrapper()
		{	
			Core.Initialize();

			FrameDataQueue.Limit = 5;
			libvlc = new LibVLC();
			player = new MediaPlayer(libvlc);

			Pitch = Align(Width * BytePerPixel);
			Lines = Align(Height);

			player.SetVideoFormat("RV24", Width, Height, Pitch);
			player.SetVideoCallbacks(Lock, null, Display);

			uint Align(uint size)
			{
				if (size % 32 == 0)
				{
					return size;
				}

				return ((size / 32) + 1) * 32; // Align on the next multiple of 32
			}
		}

		public string GetMrl()
		{
			var media = player.Media;
			if (media == null) return "None";
			return media.Mrl;
		}

		public float GetTimePosition()
		{
			return (float)player.Time / 1000;
		}

		public float GetLength()
		{
			return (float)player.Length / 1000;
		}

		public void Resume()
		{
			player.SetPause(false);
		}

		public void Pause()
		{
			player.SetPause(true);
		}

		public void Stop()
		{
			player.Stop();
		}

		public void Seek(float time)
		{
			player.SeekTo(TimeSpan.FromMilliseconds(time));
		}

		public void Load(string path)
		{
			using var media = new Media(libvlc, path);
			media.AddOption(":no-audio");
			player.Play(media);
		}

		public async void LoadUri(string uri)
		{
			Console.WriteLine(uri);
			if (uri.Contains("youtube.com") || uri.Contains("youtu.be"))
			{
				Console.WriteLine("Contains youtube");
				// get youtube streams if we can
				try
				{
					// i stole this
					using (var client = new HttpClient())
					{
						Console.WriteLine("Trying get");
						var videoPageContent = await client.GetStringAsync(uri);
						Console.WriteLine("Got");

						var regex = new Regex(@"ytInitialPlayerResponse\s*=\s*(\{.+?\})\s*;", RegexOptions.Multiline);
						var match = regex.Match(videoPageContent);

						if (!match.Success)
						{
							Console.WriteLine("uh oh");
							return;
						}

						var json = match.Result("$1");
						var playerResponseJson = JToken.Parse(json);
						var formats = playerResponseJson.SelectToken("streamingData.formats").ToList();

						// find lowest quality
						JToken? lowest = null;
						int? lowestSize = null;
						formats.ForEach(format =>
						{
							var width = format.Value<int?>("width");
							var height = format.Value<int?>("height");

							if (width == null || height == null) return;

							if (lowest == null || lowestSize == null || width * height < lowestSize)
							{
								lowest = format;
								lowestSize = width * height;
							}
						});

						if (lowest != null)
						{
							var lowestUrl = lowest.Value<string?>("url");
							if (lowestUrl != null)
							{
								Console.WriteLine(lowestUrl);
								uri = lowestUrl;
							}
							else
							{
								Console.WriteLine(json);
							}
						}
						else
						{
							Console.WriteLine(json);
						}
					}
				}
				catch (Exception ex)
				{
					var c = Console.ForegroundColor;
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine(ex.Message);
					Console.ForegroundColor = c;
				}
			}
			else
			{
				Console.WriteLine("Not youtube");
			}
			
			using var media = new Media(libvlc, new Uri(uri));
			media.AddOption(":no-audio");
			player.Play(media);
		}
	}
}
