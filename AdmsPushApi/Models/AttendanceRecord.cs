namespace AdmsPushApi.Models
{
    public class AttendanceRecord
    {
        public int Id { get; set; }
        public string? DeviceSerialNumber { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int Status { get; set; }
        public int VerifyMode { get; set; }
        public int WorkCode { get; set; }
    }
}
