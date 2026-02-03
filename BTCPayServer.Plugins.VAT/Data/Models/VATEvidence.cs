using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.VAT.Data.Models;

public enum VATEvidenceType
{
    BillingAddress = 0,
    IPGeolocation = 1,
    BankCountry = 2,
    PhoneNumber = 3
}

public class VATEvidence
{
    [Key]
    public int Id { get; set; }

    [MaxLength(50)]
    public string VATInvoiceRecordId { get; set; } = string.Empty;

    public VATEvidenceType Type { get; set; }

    /// <summary>
    /// Country code determined from this evidence (ISO 3166-1 alpha-2)
    /// </summary>
    [MaxLength(2)]
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>
    /// Confidence level of this evidence (0.0 to 1.0)
    /// </summary>
    [Column(TypeName = "decimal(3,2)")]
    public decimal Confidence { get; set; } = 1.0m;

    /// <summary>
    /// Raw data supporting this evidence (e.g., full address, IP address)
    /// </summary>
    public string? RawData { get; set; }

    public DateTimeOffset CollectedAt { get; set; } = DateTimeOffset.UtcNow;

    [ForeignKey(nameof(VATInvoiceRecordId))]
    public virtual VATInvoiceRecord? VATInvoiceRecord { get; set; }
}
