namespace FcmsPro.Core.Entities;

/// <summary>
/// One row per named sequence ("quote_seq" | "invoice_seq" | "receipt_seq").
/// CounterService.NextAsync() increments this transactionally to produce
/// human-facing numbers like QUO-00001.
/// </summary>
public class Counter
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}
