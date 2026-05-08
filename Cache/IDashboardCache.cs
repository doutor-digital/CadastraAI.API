namespace CadastraAI.API.Cache;

public interface IDashboardCache
{
    Task<T> GetOrSetAsync<T>(
        Guid empresaId,
        string keySuffix,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken ct) where T : class;

    Task InvalidateEmpresaAsync(Guid empresaId, CancellationToken ct);
}
