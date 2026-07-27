using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;
using MiniPos.Models;

namespace MiniPos.Services
{
	public class SerialReceiptPrinter : IDisposable
	{
		private SerialPort? _serialPort;
		private readonly Encoding _koreanEncoding;

		private static readonly byte[] CMD_INIT = { 0x1B, 0x40 };             // 초기화
		private static readonly byte[] CMD_ALIGN_CENTER = { 0x1B, 0x61, 1 };  // 가운데 정렬
		private static readonly byte[] CMD_ALIGN_LEFT = { 0x1B, 0x61, 0 };    // 좌측 정렬
		private static readonly byte[] CMD_BOLD_ON = { 0x1B, 0x45, 1 };       // 굵게 켜기
		private static readonly byte[] CMD_BOLD_OFF = { 0x1B, 0x45, 0 };      // 굵게 끄기
		private static readonly byte[] CMD_CUT_PAPER = { 0x1D, 0x56, 0x41, 0x10 }; // 용지 자동 컷팅

		public SerialReceiptPrinter()
		{
			_koreanEncoding = Encoding.GetEncoding("EUC-KR");
		}

		public void Open(string portName = "COM1", int baudRate = 9600)
		{
			Close();

			_serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
			{
				WriteTimeout = 3000,
				ReadTimeout = 3000
			};

			_serialPort.Open();
		}

		public async Task PrintReceiptAsync(IEnumerable<Product> products, decimal totalAmount)
		{
			if (_serialPort == null || !_serialPort.IsOpen)
				throw new InvalidOperationException("프린터 포트(COM Port)가 열려있지 않습니다.");

			using MemoryStream ms = new();

			ms.Write(CMD_INIT);
			ms.Write(CMD_ALIGN_CENTER);
			ms.Write(CMD_BOLD_ON);
			WriteText(ms, "[ 미니 POS 결제 영수증 ]\r\n\r\n");
			ms.Write(CMD_BOLD_OFF);

			// 2. 매장 정보 및 구분선
			ms.Write(CMD_ALIGN_LEFT);
			WriteText(ms, $"매장명 : WPF 실전 개발 라운지\r\n");
			WriteText(ms, $"출력일시: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n");
			WriteText(ms, "------------------------------------------\r\n");
			WriteText(ms, "상품명                수량           금액\r\n");
			WriteText(ms, "------------------------------------------\r\n");

			foreach (var item in products)
			{
				string namePart = PadRightKorean(item.Name, 18);
				string line = $"{namePart}	1   {item.Price,11:N0}원\r\n";
				WriteText(ms, line);
			}

			// 4. 합계 및 꼬리말
			WriteText(ms, "------------------------------------------\r\n");
			ms.Write(CMD_BOLD_ON);
			WriteText(ms, $"결제 합계 :              {totalAmount,15:N0}원\r\n");
			ms.Write(CMD_BOLD_OFF);
			WriteText(ms, "------------------------------------------\r\n\r\n");

			ms.Write(CMD_ALIGN_CENTER);
			WriteText(ms, "이용해 주셔서 감사합니다.\r\n");
			WriteText(ms, "Wi-Fi : WPF_DEV_LOUNGE / PW: mfc2wpf!\r\n\r\n\r\n\r\n"); // 컷팅을 위해 여백 이송

			// 5. 용지 컷팅 명령 전송
			ms.Write(CMD_CUT_PAPER);

			// 6. 생성된 전체 바이트 스트림을 포트로 전송 (백그라운드 스레드 위임)
			byte[] printData = ms.ToArray();
			await Task.Run(() => _serialPort.Write(printData, 0, printData.Length));
		}

		private void WriteText(MemoryStream ms, string text)
		{
			byte[] bytes = _koreanEncoding.GetBytes(text);
			ms.Write(bytes, 0, bytes.Length);
		}

		// 한글과 영문의 시각적 너비 차이(한글=2바이트, 영문=1바이트)를 보정하여 영수증 줄을 맞춥니다.
		private string PadRightKorean(string str, int totalByteWidth)
		{
			int currentByteCount = _koreanEncoding.GetByteCount(str);
			if (currentByteCount >= totalByteWidth)
				return str; // 초과 시 그대로 반환 (실무에서는 텍스트 자르기 적용)

			int spacesToAdd = totalByteWidth - currentByteCount;
			return str + new string(' ', spacesToAdd);
		}

		public void Close()
		{
			if( _serialPort != null && _serialPort.IsOpen)
			{
				_serialPort.Close();
			}
			_serialPort?.Dispose();
			_serialPort = null;
		}

		public void Dispose()
		{
			Close();
			GC.SuppressFinalize(this);
		}
	}
}
