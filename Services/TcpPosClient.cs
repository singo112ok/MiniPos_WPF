using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiniPos.Services
{
	public class TcpPosClient : IAsyncDisposable
	{
		private TcpClient? _client;
		private NetworkStream? _stream;
		private StreamReader? _reader;
		private StreamWriter? _writer;

		public bool IsConnected => _client?.Connected ?? false;

		public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
		{
			await DisposeAsync();

			_client = new TcpClient();

			await _client.ConnectAsync(host, port, cancellationToken);

			_stream = _client.GetStream();

			_reader = new StreamReader(_stream, Encoding.UTF8);
			_writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };
		}

		public async Task<string?> SendCommandAsync(string command, CancellationToken cancellationToken = default)
		{
			if (!IsConnected || _writer == null || _reader == null)
				throw new InvalidOperationException("서버에 연결되어 있지 않습니다.");

			//await _writer.WriteAsync(command.AsMemory(), cancellationToken);

			await _writer.WriteLineAsync(command.AsMemory(), cancellationToken);

			//return await _reader.ReadToEndAsync(cancellationToken);
			return await _reader.ReadLineAsync(cancellationToken);
		}

		public async ValueTask DisposeAsync()
		{
			_reader?.Dispose();
			_writer?.Dispose();
			if(_stream != null) await _stream.DisposeAsync();
			_client?.Dispose();

			_client = null;
			_stream = null;
			_reader = null;
			_writer = null;
		}
	}
}
