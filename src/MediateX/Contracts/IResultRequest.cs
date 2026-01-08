using MediateX;

namespace MediateX.Contracts;

/// <summary>
/// Marker interface for requests that return a <see cref="Result{T}"/>.
/// This is a convenience interface that makes the intent explicit.
/// </summary>
/// <typeparam name="T">The type of the value on success.</typeparam>
/// <remarks>
/// Usage:
/// <code>
/// // Instead of:
/// public record GetProductQuery(int Id) : IRequest&lt;Result&lt;Product&gt;&gt;;
///
/// // You can use:
/// public record GetProductQuery(int Id) : IResultRequest&lt;Product&gt;;
/// </code>
/// Both approaches are equivalent and fully supported.
/// </remarks>
public interface IResultRequest<T> : IRequest<Result<T>>;

/// <summary>
/// Marker interface for requests that return a <see cref="Result"/> (no value).
/// This is a convenience interface that makes the intent explicit.
/// </summary>
/// <remarks>
/// Usage:
/// <code>
/// // Instead of:
/// public record DeleteProductCommand(int Id) : IRequest&lt;Result&gt;;
///
/// // You can use:
/// public record DeleteProductCommand(int Id) : IResultRequest;
/// </code>
/// Both approaches are equivalent and fully supported.
/// </remarks>
public interface IResultRequest : IRequest<Result>;
