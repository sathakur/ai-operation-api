namespace AIInventory.Api.Models;

public class ChatPresentation
{
    public string ResponseType { get; set; } = "text";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<string> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public long? Total { get; set; }
}
