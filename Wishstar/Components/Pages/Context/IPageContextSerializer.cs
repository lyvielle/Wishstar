using System.Reflection;

namespace Wishstar.Components.Pages.Context {
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class PageContextItemAttribute : Attribute { }

    public static class IPageContextSerializer {
        private static Dictionary<int, Type> _ContextCache = [];
        private static readonly object _ContextCacheLock = new();

        public static string Serialize<T>(T context) where T : IPageContext {
            return Serialize(context, typeof(T));
        }

        public static string Serialize(object context, Type contextType) {
            string contextString = $"page={Uri.EscapeDataString(((IPageContext)context).Page)}";
            int contextId = GetContextTypeId(contextType);
            contextString += $"&contextId={contextId}";

            contextType.GetProperties()
                .Where(p => p.GetCustomAttribute<PageContextItemAttribute>() != null)
                .ToList()
                .ForEach(x => {
                    if (x.Name == nameof(IPageContext.Page)) {
                        return;
                    }

                    var value = x.GetValue(context);
                    if (value != null) {
                        if (typeof(IPageContext).IsAssignableFrom(x.PropertyType)) {
                            value = Uri.EscapeDataString(Serialize(value, value.GetType()));
                        }

                        string? stringValue = value.ToString();
                        if (!string.IsNullOrWhiteSpace(stringValue)) {
                            contextString += $"&{x.Name}={Uri.EscapeDataString(stringValue)}";
                        }
                    }
                });

            return contextString;
        }

        public static T Deconstruct<T>(string contextContent) where T : IPageContext, new() {
            return (T?)Deconstruct(contextContent, typeof(T)) ?? new();
        }

        public static object? Deconstruct(string contextContent, Type contextType) {
            if (typeof(IPageContext).IsAssignableFrom(contextType)) {
                contextType = GetContextType(int.Parse(contextContent.Split('&').FirstOrDefault(x => x.StartsWith("contextId="))?.Split('=')[1] ?? "-1"));
            }

            object? instance = Activator.CreateInstance(contextType)
                   ?? throw new InvalidCastException($"Failed to create instance of {contextType.FullName}");

            var contextItems = contextContent.Split('&');
            foreach (var item in contextItems) {
                var keyValue = item.Split('=');
                if (keyValue.Length == 2) {
                    var key = keyValue[0];
                    var value = Uri.UnescapeDataString(keyValue[1]);
                    if (key == "page") {
                        ((IPageContext)instance).Page = value;
                    } else if (key == "contextId") {
                        continue;
                    } else {
                        var property = contextType.GetProperty(key);
                        if (property != null) {
                            if (typeof(IPageContext).IsAssignableFrom(property.PropertyType)) {
                                var subContext = Deconstruct(Uri.UnescapeDataString(value), property.PropertyType);
                                property.SetValue(instance, subContext);
                            } else if (property.PropertyType == typeof(string)) {
                                property.SetValue(instance, value);
                            } else if (property.PropertyType.IsEnum) {
                                var enumValue = Enum.Parse(property.PropertyType, value);
                                property.SetValue(instance, enumValue);
                            } else if (property.PropertyType == typeof(bool)) {
                                var boolValue = bool.Parse(value);
                                property.SetValue(instance, boolValue);
                            } else if (property.PropertyType == typeof(int)) {
                                var intValue = int.Parse(value);
                                property.SetValue(instance, intValue);
                            } else {
                                if (property.CanWrite) {
                                    property.SetValue(instance, Convert.ChangeType(value, property.PropertyType));
                                }
                            }
                        }
                    }
                }
            }

            return instance;
        }

        private static int GetContextTypeId(Type contextType) {
            lock (_ContextCacheLock) {
                foreach (var kvp in _ContextCache) {
                    if (kvp.Value == contextType) {
                        return kvp.Key;
                    }
                }

                int newId = _ContextCache.Count + 1;
                _ContextCache[newId] = contextType;
                return newId;
            }
        }

        private static Type GetContextType(int contextId) {
            lock (_ContextCacheLock) {
                if (_ContextCache.TryGetValue(contextId, out Type? contextType)) {
                    return contextType;
                }

                throw new KeyNotFoundException($"Context type with ID {contextId} not found.");
            }
        }
    }
}