using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class SceneReferenceValidator
{
    public enum ReferenceKind
    {
        Required,
        Recommended,
        RuntimeBound,
        Optional
    }

    public readonly struct Entry
    {
        public Entry(string name, bool isAssigned, ReferenceKind kind, string note)
        {
            Name = name;
            IsAssigned = isAssigned;
            Kind = kind;
            Note = note;
        }

        public string Name { get; }
        public bool IsAssigned { get; }
        public ReferenceKind Kind { get; }
        public string Note { get; }
    }

    private static readonly HashSet<string> LoggedKeys = new HashSet<string>();

    public static Entry Required(string name, Object value, string note = null)
    {
        return new Entry(name, value != null, ReferenceKind.Required, note);
    }

    public static Entry Required(string name, bool isAssigned, string note = null)
    {
        return new Entry(name, isAssigned, ReferenceKind.Required, note);
    }

    public static Entry Recommended(string name, Object value, string note = null)
    {
        return new Entry(name, value != null, ReferenceKind.Recommended, note);
    }

    public static Entry Recommended(string name, bool isAssigned, string note = null)
    {
        return new Entry(name, isAssigned, ReferenceKind.Recommended, note);
    }

    public static Entry RuntimeBound(string name, Object value, string note = null)
    {
        return new Entry(name, value != null, ReferenceKind.RuntimeBound, note);
    }

    public static Entry RuntimeBound(string name, bool isAssigned, string note = null)
    {
        return new Entry(name, isAssigned, ReferenceKind.RuntimeBound, note);
    }

    public static void Validate(Component owner, string scope, params Entry[] entries)
    {
        ValidateInternal(owner, scope, false, entries);
    }

    public static void ValidateOnce(Component owner, string scope, params Entry[] entries)
    {
        ValidateInternal(owner, scope, true, entries);
    }

    private static void ValidateInternal(Component owner, string scope, bool logOnlyOnce, params Entry[] entries)
    {
        if (owner == null || entries == null || entries.Length == 0)
        {
            return;
        }

        List<string> missingRequired = null;
        List<string> missingRecommended = null;
        List<string> runtimeBound = null;

        for (int i = 0; i < entries.Length; i++)
        {
            Entry entry = entries[i];
            if (entry.IsAssigned)
            {
                continue;
            }

            string label = string.IsNullOrWhiteSpace(entry.Note)
                ? entry.Name
                : $"{entry.Name} ({entry.Note})";

            switch (entry.Kind)
            {
                case ReferenceKind.Required:
                    missingRequired ??= new List<string>();
                    missingRequired.Add(label);
                    break;
                case ReferenceKind.Recommended:
                    missingRecommended ??= new List<string>();
                    missingRecommended.Add(label);
                    break;
                case ReferenceKind.RuntimeBound:
                    runtimeBound ??= new List<string>();
                    runtimeBound.Add(label);
                    break;
            }
        }

        Log(owner, scope, "필수 참조 누락", missingRequired, true, logOnlyOnce);
        Log(owner, scope, "기준본 보강 필요", missingRecommended, true, logOnlyOnce);
        Log(owner, scope, "의도된 런타임 바인딩 null", runtimeBound, false, logOnlyOnce);
    }

    private static void Log(Component owner, string scope, string category, List<string> entries, bool warning, bool logOnlyOnce)
    {
        if (entries == null || entries.Count == 0)
        {
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.Append("[SceneReferenceValidator] ");
        builder.Append(scope);
        builder.Append(" @ ");
        builder.Append(owner.gameObject.name);
        builder.Append(" -> ");
        builder.Append(category);
        builder.Append(": ");
        builder.Append(string.Join(", ", entries));

        string message = builder.ToString();
        string key = owner.GetInstanceID() + ":" + category + ":" + message;

        if (logOnlyOnce && !LoggedKeys.Add(key))
        {
            return;
        }

        if (warning)
        {
            Debug.LogWarning(message, owner);
            return;
        }

        Debug.Log(message, owner);
    }
}
