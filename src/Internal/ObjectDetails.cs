using System;
using System.Collections.Generic;

namespace MediateX.Internal;

internal class ObjectDetails : IComparer<ObjectDetails>
{
    public string Name { get; }

    public string? AssemblyName { get; }

    public string? Location { get; }

    public object Value { get; }

    public Type Type { get; }

    public bool IsOverridden { get; set; }

    public ObjectDetails(object value)
    {
        Value = value;
        Type = Value.GetType();
        var exceptionHandlerType = value.GetType();

        Name = exceptionHandlerType.Name;
        AssemblyName = exceptionHandlerType.Assembly.GetName().Name;
        Location = exceptionHandlerType.Namespace?.Replace($"{AssemblyName}.", string.Empty);
    }

    public int Compare(ObjectDetails? x, ObjectDetails? y) => (x, y) switch
    {
        (null, _) => 1,
        (_, null) => -1,
        _ => CompareByAssembly(x, y) ?? CompareByNamespace(x, y) ?? CompareByLocation(x, y)
    };

    /// <summary>
    /// Compare two objects according to current assembly
    /// </summary>
    /// <param name="x">First object to compare</param>
    /// <param name="y">Second object to compare</param>
    /// <returns>
    /// An object has a higher priority if it belongs to the current assembly and the other is not;
    /// If none of the objects belong to the current assembly, they can be considered equal;
    /// If both objects belong to the current assembly, they can't be compared only by this criterion.
    /// </returns>
    private int? CompareByAssembly(ObjectDetails x, ObjectDetails y) =>
        (x.AssemblyName == AssemblyName, y.AssemblyName == AssemblyName) switch
        {
            (true, false) => -1,
            (false, true) => 1,
            (false, false) => 0,
            _ => null
        };

    /// <summary>
    /// Compare two objects according to current namespace
    /// </summary>
    /// <param name="x">First object to compare</param>
    /// <param name="y">Second object to compare</param>
    /// <returns>
    /// An object has a higher priority if it belongs to the current/child namespace and the other is not;
    /// If both objects belong to the current/child namespace, they can be considered equal;
    /// If none of the objects belong to the current/child namespace, they can't be compared by this criterion.
    /// </returns>
    private int? CompareByNamespace(ObjectDetails x, ObjectDetails y)
    {
        if (Location is null || x.Location is null || y.Location is null)
            return 0;

        return (x.Location.StartsWith(Location, StringComparison.Ordinal),
                y.Location.StartsWith(Location, StringComparison.Ordinal)) switch
        {
            (true, false) => -1,
            (false, true) => 1,
            (true, true) => 0,
            _ => null
        };
    }

    /// <summary>
    /// Compare two objects according to location in the assembly
    /// </summary>
    /// <param name="x">First object to compare</param>
    /// <param name="y">Second object to compare</param>
    /// <returns>
    /// An object has a higher priority if it location is part of the current location and the other is not;
    /// If both objects are part of the current location, the closest has higher priority;
    /// If none of the objects are part of the current location, they can be considered equal.
    /// </returns>
    private int CompareByLocation(ObjectDetails x, ObjectDetails y)
    {
        if (Location is null || x.Location is null || y.Location is null)
            return 0;

        return (Location.StartsWith(x.Location, StringComparison.Ordinal),
                Location.StartsWith(y.Location, StringComparison.Ordinal),
                x.Location.Length.CompareTo(y.Location.Length)) switch
        {
            (true, false, _) => -1,
            (false, true, _) => 1,
            (_, _, < 0) => 1,
            (_, _, > 0) => -1,
            _ => 0
        };
    }
}