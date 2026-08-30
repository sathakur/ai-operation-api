namespace AIInventory.Api.Models;
public class ChatResponse
{
    public string Answer { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public string User { get; set; } = "";
    public string AgentName { get; set; } = "";
    public string AgentVersion { get; set; } = "";
}
