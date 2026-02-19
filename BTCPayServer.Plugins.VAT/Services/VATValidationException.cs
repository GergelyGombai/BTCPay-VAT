namespace BTCPayServer.Plugins.VAT.Services;

public class VATValidationException : Exception
{
    public VATValidationException(string message) : base(message) { }
}
