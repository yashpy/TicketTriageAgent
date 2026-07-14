using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TicketTriageAgent.Models;

public class Ticket
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public TicketStatus Status { get; set; } = TicketStatus.New;

    // --- classify step output ---
    public string? Category { get; set; }
    public double? ClassificationConfidence { get; set; }
    public string? ClassificationReasoning { get; set; }

    // --- retrieve step output ---
    public List<string> RetrievedContext { get; set; } = new();

    // --- draft step output ---
    public string? DraftResponse { get; set; }
    public double? DraftConfidence { get; set; }

    // --- decision ---
    public string? DecisionReason { get; set; }
    public string? RejectionReason { get; set; }

    public List<TraceStep> ReasoningTrail { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
