using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.VAT.Data.Models;

public class VATInvoiceRecord
{
    [Key]
    [MaxLength(50)]
    public string InvoiceId { get; set; } = string.Empty;

    [MaxLength(50)]
    public string StoreId { get; set; } = string.Empty;

    /// <summary>
    /// Customer's country code (ISO 3166-1 alpha-2)
    /// </summary>
    [MaxLength(2)]
    public string CustomerCountry { get; set; } = string.Empty;

    /// <summary>
    /// VAT rate applied as a percentage (e.g., 20.0 for 20%)
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal VATRate { get; set; }

    /// <summary>
    /// Net amount before VAT in the invoice currency
    /// </summary>
    [Column(TypeName = "decimal(18,8)")]
    public decimal NetAmount { get; set; }

    /// <summary>
    /// VAT amount in the invoice currency
    /// </summary>
    [Column(TypeName = "decimal(18,8)")]
    public decimal VATAmount { get; set; }

    /// <summary>
    /// Gross amount including VAT in the invoice currency
    /// </summary>
    [Column(TypeName = "decimal(18,8)")]
    public decimal GrossAmount { get; set; }

    /// <summary>
    /// Invoice currency code (e.g., "EUR", "USD")
    /// </summary>
    [MaxLength(10)]
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Whether reverse charge was applied (B2B transaction with validated VAT number)
    /// </summary>
    public bool ReverseChargeApplied { get; set; }

    /// <summary>
    /// Customer's VAT number if provided and validated
    /// </summary>
    [MaxLength(20)]
    public string? CustomerVATNumber { get; set; }

    /// <summary>
    /// VAT mode that was applied (Fixed or OSS)
    /// </summary>
    public VATMode ModeApplied { get; set; }

    /// <summary>
    /// JSON serialized evidence data for compliance
    /// </summary>
    public string? EvidenceJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public virtual ICollection<VATEvidence> Evidence { get; set; } = new List<VATEvidence>();
}
