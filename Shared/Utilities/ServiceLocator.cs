using System;
using System.Collections.Generic;

namespace CodexVS22.Shared.Utilities
{
    /// <summary>
    /// Minimal service registry used while the refactor migrates to full DI.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly object Gate = new();
        private static readonly Dictionary<Type, object> Services = new();

        public static void RegisterSingleton<TService>(Func<TService> factory)
            where TService : class
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            lock (Gate)
            {
                if (Services.TryGetValue(typeof(TService), out var existing) && existing is TService)
                {
                    return;
                }

                var instance = factory();
                if (instance == null)
                    throw new InvalidOperationException($"Factory for {typeof(TService).FullName} returned null.");

                Services[typeof(TService)] = instance;
            }
        }

        public static TService GetRequiredService<TService>()
            where TService : class
        {
            lock (Gate)
            {
                if (Services.TryGetValue(typeof(TService), out var instance) && instance is TService typed)
                {
                    return typed;
                }
            }

            throw new InvalidOperationException($"Service {typeof(TService).FullName} is not registered.");
        }

        public static bool TryGetService<TService>(out TService service)
            where TService : class
        {
            lock (Gate)
            {
                if (Services.TryGetValue(typeof(TService), out var instance) && instance is TService typed)
                {
                    service = typed;
                    return true;
                }
            }

            service = null;
            return false;
        }

        public static void ResetForTesting()
        {
            lock (Gate)
            {
                Services.Clear();
            }
        }
    }
}
