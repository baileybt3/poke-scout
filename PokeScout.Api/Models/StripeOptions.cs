namespace PokeScout.Api.Models
{
    public class StripeOptions
    {
        public string SecretKey { get; set; } = string.Empty;
        public string FrontendBaseUrl { get; set; } = string.Empty;
    }
}