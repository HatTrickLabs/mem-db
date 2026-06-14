using System;

public class BenchmarkRecord
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;      // ~16 chars
    public string Category { get; set; } = string.Empty;  // ~8 chars
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public bool IsActive { get; set; }
}
