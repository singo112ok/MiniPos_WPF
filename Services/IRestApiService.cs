using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MiniPos.Models;

namespace MiniPos.Services
{
    public interface IRestApiService
    {
        Task<List<ApiPost>?> GetPostsAsync();
    }
}
