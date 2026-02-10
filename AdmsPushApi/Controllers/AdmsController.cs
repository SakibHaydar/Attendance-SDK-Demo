using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;

namespace AdmsPushApi.Controllers
{
    [ApiController]
    [Route("iclock")]
    public class AdmsController : ControllerBase
    {
        // 1. Handshake: Device checks in
        [HttpGet("cdata")]
        public string Handshake([FromQuery] string SN)
        {
            Console.WriteLine($"[ADMS] Handshake from Device SN: {SN}");
            return "OK";
        }

        // 2. Data Push: Device sends attendance records
        [HttpPost("cdata")]
        public async Task<string> ReceiveData([FromQuery] string SN)
        {
            using var reader = new StreamReader(Request.Body);
            var content = await reader.ReadToEndAsync();
            
            Console.WriteLine($"[ADMS] Received Logs from SN {SN}:");
            Console.WriteLine(content);
            
            // Logic: Parse 'content' and save to your Database here
            
            return "OK";
        }
    }
}
