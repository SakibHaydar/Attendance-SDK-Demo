using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;

namespace AdmsPushApi.Controllers
{ 
    [ApiController]
    [Route("iclock")]
    public class AdmsController : ControllerBase
    {
        // 1. Handshake / Initialization (Device checks server parameters)
        [HttpGet("cdata")]
        public IActionResult Handshake([FromQuery] string SN)
        {
            Console.WriteLine($"[ADMS] Handshake from Device SN: {SN}");
            // Required ADMS Configuration format.
            // TransFlag: specifies which logs to send (1111000000 means all logs)
            // Realtime: 1 means immediate push upon swipe
            // Encrypt: 0 means no encryption
            string response = $"GET OPTION FROM: {SN}\r\nStamp=9999\r\nOpStamp=9999\r\nPhotoStamp=9999\r\nErrorDelay=60\r\nDelay=30\r\nTransTimes=00:00;14:00\r\nTransInterval=1\r\nTransFlag=1111000000\r\nRealtime=1\r\nEncrypt=0";
            
            return Content(response, "text/plain");
        }

        // 2. Data Push (Device sends attendance or operlogs)
        [HttpPost("cdata")]
        public async Task<IActionResult> ReceiveData([FromQuery] string SN, [FromQuery] string? table = null)
        {
            using var reader = new StreamReader(Request.Body);
            var content = await reader.ReadToEndAsync();
            
            Console.WriteLine($"[ADMS] Received Data [{table}] from SN {SN}:");
            Console.WriteLine(content);
            
            // TODO: Parse 'content' according to the 'table' type (ATTLOG, OPERLOG) and save to DB
            
            // The device requires a literal "OK" followed by a newline to acknowledge receipt
            return Content("OK\r\n", "text/plain");
        }

        // 3. Command Polling (Device asks: "Do you have any commands for me to run?")
        [HttpGet("getrequest")]
        public IActionResult GetRequest([FromQuery] string SN)
        {
            // If you had commands (like 'Reboot' or 'Add User'), you'd return them here.
            // Returning "OK" tells the device there are no pending commands.
            return Content("OK\r\n", "text/plain");
        }

        // 4. Command Result (Device reports back the result of a command)
        [HttpPost("devicecmd")]
        public async Task<IActionResult> DeviceCmd([FromQuery] string SN)
        {
            using var reader = new StreamReader(Request.Body);
            var content = await reader.ReadToEndAsync();
            
            Console.WriteLine($"[ADMS] Command Result from SN {SN}:");
            Console.WriteLine(content);
            
            return Content("OK\r\n", "text/plain");
        }
    }
}
