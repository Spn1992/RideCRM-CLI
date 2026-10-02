namespace AmusementRideCRM.Models;

public class ErrorLogging
{
    public string ErrorLogId { get; set; }
    public DateTime ErrorDate { get; set; }
    public string Severity { get; set; }
    public string? Source { get; set; }
    public string? MachineName { get; set; }
    public string? UserName { get; set; }
    public string? RequestPath { get; set; }
    public string? RequestMethod { get; set; }
    public string? QueryString { get; set; }
    public int? StatusCode { get; set; }
    public string ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
    public string? InnerException { get; set; }
    public string? AdditionalData { get; set; }
    public Guid? CorrelationId { get; set; }
}
