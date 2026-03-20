namespace PokeScout.Api.Dtos;

public sealed class CatalogImageResult
{
    public byte[] ImageBytes { get; }
    public string ContentType { get; }

    public CatalogImageResult(byte[] imageBytes, string contentType)
    {
        ImageBytes = imageBytes;
        ContentType = contentType;
    }
}