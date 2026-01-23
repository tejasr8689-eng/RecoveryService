namespace RecoveryService.Models
{
    public class RecoveryRequest
    {
        public RecoveryAction Action { get; set; }
        public required string Target { get; set; } = string.Empty;
    }
}
