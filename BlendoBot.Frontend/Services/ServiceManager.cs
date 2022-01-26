using BlendoBot.Core.Services;
using System;
using System.Collections.Generic;

namespace BlendoBot.Frontend.Services;

/// <summary>
/// Manages all services underneath the bot. Services must implement the <see cref="IBotService"/> interface in
/// some way. Services must be uniquely registered under all types that derive from <see cref="IBotService"/> (e.g.
/// if type A implemented interfaces B and C, which both implemented <see cref="IBotService"/>, the service would
/// be registered as A, B, and C, and no other class can be registered that also implements either A, B, or C).
/// </summary>
internal class ServiceManager {
	private readonly Dictionary<Type, IBotService> services = new();

	/// <summary>
	/// Registers a service within the <see cref="ServiceManager"/>. The service will be referable by all types it
	/// implements that eventually implement <see cref="IBotService"/>.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="service"></param>
	public void RegisterService(IBotService service) {
		foreach (Type serviceType in GetAllBotServiceTypes(service.GetType())) {
			if (services.ContainsKey(serviceType)) {
				throw new ArgumentException($"Service of type {serviceType} already registered");
			} else {
				services.Add(serviceType, service);
			}
		}
	}

	/// <summary>
	/// Returns the instance of a service that is, extends, or implements <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	public T GetService<T>() where T : IBotService {
		if (services.ContainsKey(typeof(T))) {
			return (T)services[typeof(T)];
		} else {
			throw new ArgumentException($"Service of type {typeof(T)} cannot be found");
		}
	}

	/// <summary>
	/// Returns the instance of a service that is, extends, or implements <paramref name="t"/>. It is preferable to
	/// use <see cref="GetService{T}"/> when possible.
	/// </summary>
	/// <param name="t"></param>
	/// <returns></returns>
	public IBotService GetService(Type t) {
		if (services.ContainsKey(t)) {
			return services[t];
		} else {
			throw new ArgumentException($"Service of type {t} cannot be found");
		}
	}

	/// <summary>
	/// Returns an list of all types including and extended/implemented by <paramref name="t"/> that root from
	/// <see cref="IBotService"/>. If the type does not implement <see cref="IBotService"/> in any way, this array
	/// should be empty.
	/// </summary>
	/// <param name="t"></param>
	/// <returns></returns>
	private static List<Type> GetAllBotServiceTypes(Type t) {
		List<Type> l = new();
		if (t != typeof(IBotService) && typeof(IBotService).IsAssignableFrom(t)) {
			l.Add(t);
			if (t.BaseType != null) {
				l.AddRange(GetAllBotServiceTypes(t.BaseType));
			}
			foreach (Type derivedInterface in t.GetInterfaces()) {
				l.AddRange(GetAllBotServiceTypes(derivedInterface));
			}
		}
		return l;
	}
}
