using MiniPos.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MiniPos.Services
{
	public class RestApiService : IRestApiService
	{
		private static readonly HttpClient _httpClient = new()
		{
			Timeout = TimeSpan.FromSeconds(10)
		};

		private const string BASE_URL = "https://jsonplaceholder.typicode.com/posts";

		public async Task<List<ApiPost>?> GetPostsAsync()
		{
			try
			{
				HttpResponseMessage response = await _httpClient.GetAsync(BASE_URL);

				response.EnsureSuccessStatusCode();

				using var contentStream = await response.Content.ReadAsStreamAsync();

				var options = new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				};

				List<ApiPost>? posts = await JsonSerializer.DeserializeAsync<List<ApiPost>>(contentStream, options);
				return posts;
			}
			catch (HttpRequestException ex)
			{
				throw new Exception($"네트워크 요청 실패: {ex.Message}", ex);
			}
			catch(JsonException ex)
			{
				throw new Exception($"JSON 데이터 파싱 실패: {ex.Message}", ex);
			}
		}
	}
}
