using System;
using System.Threading.Tasks;

namespace MoyuLinuxdo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            MoyuApp app = new MoyuApp();
            await app.Run();
        }
    }
}
