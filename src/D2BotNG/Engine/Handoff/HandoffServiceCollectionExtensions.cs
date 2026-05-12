namespace D2BotNG.Engine.Handoff;

public static class HandoffServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton service and also exposes it as <see cref="IHandoffParticipant"/>
    /// so the <see cref="HandoffManager"/> picks it up. Both registrations resolve to the
    /// same instance.
    /// </summary>
    public static IServiceCollection AddSingletonWithHandoff<TService>(this IServiceCollection services)
        where TService : class, IHandoffParticipant
    {
        services.AddSingleton<TService>();
        services.AddSingleton<IHandoffParticipant>(sp => sp.GetRequiredService<TService>());
        return services;
    }
}
