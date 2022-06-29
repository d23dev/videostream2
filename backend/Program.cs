using System;

namespace VideoStreamBackend
{
	public class Program
	{
		public static async Task Main(string[] args)
		{
			var player = new MediaPlayerWrapper();
			new Server(player);

			await Task.Delay(-1);
		}
	}
}