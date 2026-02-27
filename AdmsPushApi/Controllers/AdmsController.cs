using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using System;
using AdmsPushApi.Data;
using AdmsPushApi.Models;
using System.Linq;
using System.Collections.Concurrent;

namespace AdmsPushApi.Controllers
{ 
    [ApiController]
    [Route("iclock")]
    public class AdmsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private static readonly ConcurrentDictionary<string, string> _pendingCommands = new(StringComparer.OrdinalIgnoreCase);

        private void LogActivity(string message)
        {
            try
            {
                var logMsg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                System.IO.File.AppendAllText("device_log.txt", logMsg);
            }
            catch { }
        }

        public AdmsController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // 1. Handshake / Initialization (Device checks server parameters)
        [HttpGet("cdata")]
        public IActionResult Handshake([FromQuery] string SN)
        {
            LogActivity($"Handshake from: {SN}");
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
            
            LogActivity($"ReceiveData [{table}] from {SN}. Content length: {content.Length}");
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
            LogActivity($"GetRequest (Poll) from {SN}");
            if (_pendingCommands.TryRemove(SN, out var command))
            {
                LogActivity($"Sending Command to {SN}: {command}");
                Console.WriteLine($"[ADMS] Sending Queued Command to SN {SN}: {command}");
                return Content(command, "text/plain");
            }

            // Returning "OK" tells the device there are no pending commands.
            return Content("OK\r\n", "text/plain");
        }

        // New Endpoint: Request the device to sync historical data from a specific date
        // Example: GET /iclock/requestsync?sn=JYM6244900194&date=2026-02-20
        [HttpGet("requestsync")]
        public IActionResult RequestSync([FromQuery] string sn, [FromQuery] string date)
        {
            if (string.IsNullOrEmpty(sn) || !DateTime.TryParse(date, out var targetDate))
            {
                return BadRequest("Invalid SN or date.");
            }

            string dateStr = targetDate.ToString("yyyy-MM-dd HH:mm:ss");
            string command = $"C:881:DATA QUERY ATTLOG StartTime={dateStr}";
            _pendingCommands[sn] = command;

            return Ok(new { message = $"Sync command for SN {sn} queued starting from {dateStr}. Device will execute this upon next poll." });
        }

        [HttpGet("viewlog")]
        public IActionResult ViewLog()
        {
            if (!System.IO.File.Exists("device_log.txt")) return Content("Log file is empty.");
            return PhysicalFile(Path.Combine(Directory.GetCurrentDirectory(), "device_log.txt"), "text/plain");
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

        // New endpoint: Get attendance records filtered by a specific date (YYYY-MM-DD)
        // Example: GET /iclock/recordsbydate?date=2026-02-26
        [HttpGet("recordsbydate")]
        public IActionResult GetRecordsByDate([FromQuery] string date)
        {
            if (!DateTime.TryParse(date, out var targetDate))
            {
                return BadRequest("Invalid date format. Use YYYY-MM-DD.");
            }
            // Filter records where the date part matches the target date
            var records = _dbContext.AttendanceRecords
                .Where(r => r.Timestamp.Date == targetDate.Date)
                .OrderByDescending(r => r.Timestamp)
                .ToList();
            return Ok(records);
        }
    }
}
