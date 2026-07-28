namespace EasyVetClinic.Api.Data;

public sealed class CurrentClinic(IConfiguration configuration)
{
    public string Id => configuration["CurrentClinic:Id"]
        ?? throw new InvalidOperationException("CurrentClinic:Id must be configured.");
}