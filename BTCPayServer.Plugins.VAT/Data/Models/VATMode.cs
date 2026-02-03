namespace BTCPayServer.Plugins.VAT.Data.Models;

public enum VATMode
{
    /// <summary>
    /// Single country VAT registration - always applies home country rate
    /// </summary>
    Fixed = 0,

    /// <summary>
    /// One-Stop Shop - applies VAT rate based on customer location
    /// </summary>
    OSS = 1
}
