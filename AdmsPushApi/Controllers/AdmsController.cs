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
        private static readonly ConcurrentDictionary<string, DateTime> _lastSeen = new(StringComparer.OrdinalIgnoreCase);

        public AdmsController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private void UpdateLastSeen(string sn)
        {
            if (!string.IsNullOrEmpty(sn))
                _lastSeen[sn] = DateTime.Now;
        }

        private void LogActivity(string message)
        {
            try
            {
                var logMsg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                System.IO.File.AppendAllText("device_log.txt", logMsg);
            }
            catch { }
        }

        // 1. Handshake (Device checks server parameters)
        [HttpGet("cdata")]
        public IActionResult Handshake([FromQuery] string SN)
        {
            UpdateLastSeen(SN);
            LogActivity($"Handshake from: {SN}");
            Console.WriteLine($"[!!!] HANDSHAKE from SN: {SN}");
            string response = $"GET OPTION FROM: {SN}\r\nStamp=9999\r\nOpStamp=9999\r\nPhotoStamp=9999\r\nErrorDelay=60\r\nDelay=30\r\nTransTimes=00:00;14:00\r\nTransInterval=1\r\nTransFlag=1111000000\r\nRealtime=1\r\nEncrypt=0";
            return Content(response, "text/plain");
        }

        // 2. Data Push (Device sends attendance logs)
        [HttpPost("cdata")]
        public async Task<IActionResult> ReceiveData([FromQuery] string SN, [FromQuery] string? table = null)
        {
            using var reader = new StreamReader(Request.Body);
            var content = await reader.ReadToEndAsync();

            UpdateLastSeen(SN);
            LogActivity($"ReceiveData [{table}] from {SN}. Length: {content.Length}");
            Console.WriteLine($"[!!!] DATA RECEIVED [{table}] from SN: {SN}");
            Console.WriteLine(content);

            if (table == "ATTLOG" || string.IsNullOrEmpty(table))
            {
                var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                int saved = 0;
                foreach (var line in lines)
                {
                    var fields = line.Split('\t');
                    if (fields.Length >= 5)
                    {
                        if (DateTime.TryParse(fields[1], out var ts) &&
                            int.TryParse(fields[2], out var status) &&
                            int.TryParse(fields[3], out var verify) &&
                            int.TryParse(fields[4], out var workCode))
                        {
                            _dbContext.AttendanceRecords.Add(new AttendanceRecord
                            {
                                DeviceSerialNumber = SN,
                                UserId = fields[0],
                                Timestamp = ts,
                                Status = status,
                                VerifyMode = verify,
                                WorkCode = workCode
                            });
                            saved++;
                        }
                    }
                }
                await _dbContext.SaveChangesAsync();
                Console.WriteLine($"[!!!] Saved {saved} records.");
            }

            return Content("OK\r\n", "text/plain");
        }

        // 3. Command Polling (Device asks for pending commands)
        [HttpGet("getrequest")]
        public IActionResult GetRequest([FromQuery] string SN)
        {
            UpdateLastSeen(SN);
            LogActivity($"Poll from: {SN}");
            if (_pendingCommands.TryRemove(SN, out var command))
            {
                LogActivity($"Sending command to {SN}: {command}");
                Console.WriteLine($"[!!!] SENDING COMMAND to {SN}: {command}");
                return Content(command, "text/plain");
            }
            return Content("OK\r\n", "text/plain");
        }

        // 4. Command Result (Device reports result)
        [HttpPost("devicecmd")]
        public async Task<IActionResult> DeviceCmd([FromQuery] string SN)
        {
            using var reader = new StreamReader(Request.Body);
            var content = await reader.ReadToEndAsync();
            Console.WriteLine($"[!!!] CMD RESULT from {SN}: {content}");
            return Content("OK\r\n", "text/plain");
        }

        // 5. Request on-demand historical sync from a specific date
        // GET /iclock/requestsync?sn=JYM6244900194&date=2026-02-26
        [HttpGet("requestsync")]
        public IActionResult RequestSync([FromQuery] string sn, [FromQuery] string date)
        {
            if (string.IsNullOrEmpty(sn) || !DateTime.TryParse(date, out var targetDate))
                return BadRequest("Invalid SN or date.");

            _lastSeen.TryGetValue(sn, out var lastSeenTime);
            bool isOnline = lastSeenTime != DateTime.MinValue && (DateTime.Now - lastSeenTime).TotalMinutes < 2;

            string dateStr = targetDate.ToString("yyyy-MM-dd HH:mm:ss");
            string command = $"C:881:DATA QUERY ATTLOG StartTime={dateStr}";
            _pendingCommands[sn] = command;

            return Ok(new
            {
                isOnline,
                lastSeen = lastSeenTime == DateTime.MinValue ? "Never" : lastSeenTime.ToString("yyyy-MM-dd HH:mm:ss"),
                message = isOnline
                    ? $"Device {sn} is online. Sync command queued - data should arrive within 30 seconds."
                    : $"Warning: Device {sn} appears OFFLINE (Last seen: {(lastSeenTime == DateTime.MinValue ? "Never" : lastSeenTime.ToString("yyyy-MM-dd HH:mm:ss"))}). Command queued for when it reconnects."
            });
        }

        // 6. View device activity log
        [HttpGet("viewlog")]
        public IActionResult ViewLog()
        {
            if (!System.IO.File.Exists("device_log.txt"))
                return Content("Log file is empty. No device has connected yet.");
            return PhysicalFile(Path.Combine(Directory.GetCurrentDirectory(), "device_log.txt"), "text/plain");
        }

        // 7. Get all attendance records (JSON)
        [HttpGet("records")]
        public IActionResult GetRecords()
        {
            var records = _dbContext.AttendanceRecords
                .OrderByDescending(r => r.Timestamp)
                .ToList();
            return Ok(records);
        }

        // 8. Get attendance records for a specific date
        // GET /iclock/recordsbydate?date=2026-02-26
        [HttpGet("recordsbydate")]
        public IActionResult GetRecordsByDate([FromQuery] string date)
        {
            if (!DateTime.TryParse(date, out var targetDate))
                return BadRequest("Invalid date format. Use YYYY-MM-DD.");

            var records = _dbContext.AttendanceRecords
                .Where(r => r.Timestamp.Date == targetDate.Date)
                .OrderByDescending(r => r.Timestamp)
                .ToList();
            return Ok(records);
        }
    }
}
