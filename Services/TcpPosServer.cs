using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPos.Services
{
	public class TcpPosServer : IAsyncDisposable
	{
		private TcpListener? _listener;
		private CancellationTokenSource? _cts;
		private bool _isRunning = false;

		public bool IsRunning => _isRunning;

		public void Start(int port = 9000)
		{
			if (_isRunning) return;

			_cts = new CancellationTokenSource();
			_listener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
			_listener.Start();
			_isRunning = true;

			Task.Run(() => AcceptLoopAsync(_cts.Token));
		}

		private async Task AcceptLoopAsync(CancellationToken token)
		{
			try
			{
				while (!token.IsCancellationRequested && _listener != null)
				{
					TcpClient client = await _listener.AcceptTcpClientAsync(token);

					_ = Task.Run(() => HandleClientAsync(client, token), token);
				}
			}
			catch (OperationCanceledException) { }
			catch (Exception ex) { }
		}

		private async Task HandleClientAsync(TcpClient client, CancellationToken token)
		{
			using (client)
			using (NetworkStream stream = client.GetStream())
			using (StreamReader reader = new(stream, Encoding.UTF8))
			using (StreamWriter writer = new(stream, Encoding.UTF8) { AutoFlush = true })
			{
				while (!token.IsCancellationRequested && client.Connected)
				{
					try
					{
						string? request = await reader.ReadLineAsync(token);
						if (request == null) break;

						await Task.Delay(1000, token);

						string[] parts = request.Split('|');
						string response;
						if (parts.Length >= 2 && parts[0] == "PAY_REQ")
						{
							string amount = parts[1];
							string approvalNum = new Random().Next(10000000, 99999999).ToString();
							response = $"PAY_RES|SUCCESS|{amount}원 승인완료|승인번호:{approvalNum}";
						}
						else
						{
							response = "PAY_RES|FAIL|잘못된 요청 패킷입니다.";
						}

						await writer.WriteLineAsync(response.AsMemory(), token);
					}
					catch { break; }
				}
			}
		}

		public void Stop()
		{
			_isRunning = false;
			_cts?.Cancel();
			_listener?.Stop();
		}

		public async ValueTask DisposeAsync()
		{
			Stop();
			_cts?.Dispose();
			await Task.CompletedTask;
		}
	}
}
