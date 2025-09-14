namespace CTCare.Application.Interfaces;
/// <summary>
/// Composite cache service exposing both basic and advanced operations.
/// </summary>
public interface ICacheService: IBasicCacheService, IRedisAdvancedCacheService { }
