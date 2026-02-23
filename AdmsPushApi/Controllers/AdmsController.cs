using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using System;
using AdmsPushApi.Data;
using AdmsPushApi.Models;

namespace AdmsPushApi.Controllers
{ 
    [ApiController]
    [Route("iclock")]
    public class AdmsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public AdmsController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

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
            
            if (table == "ATTLOG" || string.IsNullOrEmpty(table))
            {
                // Parse the raw tab-separated lines
                var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var fields = line.Split('\t');
                    if (fields.Length >= 6)
                    {
                        var record = new AttendanceRecord
                        {
                            DeviceSerialNumber = SN,
                            UserId = fields[0],
                            Timestamp = DateTime.Parse(fields[1]), // e.g., 2026-02-22 08:53:10
                            Status = int.Parse(fields[2]),
                            VerifyMode = int.Parse(fields[3]),
                            WorkCode = int.Parse(fields[4])
                        };
                        _dbContext.AttendanceRecords.Add(record);
                    }
                }
                await _dbContext.SaveChangesAsync();
                Console.WriteLine($"[ADMS] Saved {lines.Length} attendance records to SQLite.");
            }
            
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

        // --- Demo Helper Endpoint ---
        // View all saved attendance records in the browser as JSON
        [HttpGet("records")]
        public IActionResult GetRecords()
        {
            var records = _dbContext.AttendanceRecords.OrderByDescending(r => r.Timestamp).ToList();
            return Ok(records);
        }
    }
}
