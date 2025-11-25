# Streaming Requests

MediateX supports streaming responses using `IAsyncEnumerable<T>`, allowing you to process and return data incrementally rather than waiting for the entire result set. This is ideal for large datasets, real-time data, or progressive processing scenarios.

---

## Core Concepts

### IStreamRequest Interface

Stream requests implement `IStreamRequest<TResponse>`:

```csharp
public interface IStreamRequest<out TResponse> { }
```

**Key characteristics:**
- Returns `IAsyncEnumerable<TResponse>` instead of `Task<TResponse>`
- Items are yielded incrementally as they become available
- Supports cancellation through `CancellationToken`
- Memory efficient for large datasets

### IStreamRequestHandler Interface

Stream handlers implement `IStreamRequestHandler<TRequest, TResponse>`:

```csharp
public interface IStreamRequestHandler<in TRequest, out TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
```

---

## Basic Streaming Example

### Define a Stream Request

```csharp
public record GetProductsStreamQuery(string Category) : IStreamRequest<Product>;
```

### Implement the Handler

```csharp
public class GetProductsStreamHandler : IStreamRequestHandler<GetProductsStreamQuery, Product>
{
    private readonly IProductRepository _repository;
    private readonly ILogger<GetProductsStreamHandler> _logger;

    public GetProductsStreamHandler(
        IProductRepository repository,
        ILogger<GetProductsStreamHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async IAsyncEnumerable<Product> Handle(
        GetProductsStreamQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting to stream products in category {Category}", request.Category);

        await foreach (var product in _repository.StreamByCategoryAsync(request.Category, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogDebug("Yielding product {ProductId}", product.Id);
            yield return product;
        }

        _logger.LogInformation("Finished streaming products");
    }
}
```

### Send the Stream Request

```csharp
public class ProductService
{
    private readonly IMediator _mediator;

    public ProductService(IMediator mediator) => _mediator = mediator;

    public async Task ProcessProductsAsync(string category, CancellationToken cancellationToken)
    {
        var query = new GetProductsStreamQuery(category);

        await foreach (var product in _mediator.CreateStream(query, cancellationToken))
        {
            Console.WriteLine($"Processing: {product.Name}");
            // Process each product as it arrives
            await ProcessProductAsync(product, cancellationToken);
        }
    }
}
```

---

## Use Cases

### 1. Large Dataset Processing

Stream large datasets without loading everything into memory:

```csharp
public record ExportOrdersQuery(DateTime From, DateTime To) : IStreamRequest<OrderDto>;

public class ExportOrdersHandler : IStreamRequestHandler<ExportOrdersQuery, OrderDto>
{
    private readonly AppDbContext _dbContext;

    public ExportOrdersHandler(AppDbContext dbContext)
        => _dbContext = dbContext;

    public async IAsyncEnumerable<OrderDto> Handle(
        ExportOrdersQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var orders = _dbContext.Orders
            .Where(o => o.CreatedAt >= request.From && o.CreatedAt <= request.To)
            .AsAsyncEnumerable();

        await foreach (var order in orders.WithCancellation(cancellationToken))
        {
            yield return new OrderDto
            {
                Id = order.Id,
                Total = order.Total,
                CustomerName = order.Customer.Name,
                CreatedAt = order.CreatedAt
            };
        }
    }
}

// Usage: Stream to CSV file
await using var writer = new StreamWriter("orders.csv");
await writer.WriteLineAsync("Id,Total,Customer,Date");

await foreach (var order in _mediator.CreateStream(
    new ExportOrdersQuery(DateTime.Now.AddYears(-1), DateTime.Now)))
{
    await writer.WriteLineAsync($"{order.Id},{order.Total},{order.CustomerName},{order.CreatedAt}");
}
```

### 2. Real-Time Data Processing

Process real-time data as it becomes available:

```csharp
public record MonitorSystemMetricsQuery : IStreamRequest<SystemMetric>;

public class MonitorSystemMetricsHandler
    : IStreamRequestHandler<MonitorSystemMetricsQuery, SystemMetric>
{
    private readonly ISystemMonitor _monitor;

    public MonitorSystemMetricsHandler(ISystemMonitor monitor)
        => _monitor = monitor;

    public async IAsyncEnumerable<SystemMetric> Handle(
        MonitorSystemMetricsQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var metric = await _monitor.GetCurrentMetricAsync(cancellationToken);
            yield return metric;

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }
}
```

### 3. Progressive Search Results

Return search results as they're found:

```csharp
public record SearchProductsQuery(string SearchTerm) : IStreamRequest<ProductSearchResult>;

public class SearchProductsHandler
    : IStreamRequestHandler<SearchProductsQuery, ProductSearchResult>
{
    private readonly ISearchService _searchService;

    public SearchProductsHandler(ISearchService searchService)
        => _searchService = searchService;

    public async IAsyncEnumerable<ProductSearchResult> Handle(
        SearchProductsQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Search in database first
        await foreach (var result in _searchService.SearchDatabaseAsync(
            request.SearchTerm, cancellationToken))
        {
            yield return result;
        }

        // Then search in external catalog
        await foreach (var result in _searchService.SearchExternalCatalogAsync(
            request.SearchTerm, cancellationToken))
        {
            yield return result;
        }

        // Finally search in archive
        await foreach (var result in _searchService.SearchArchiveAsync(
            request.SearchTerm, cancellationToken))
        {
            yield return result;
        }
    }
}
```

### 4. Batch Processing

Process items in batches:

```csharp
public record ProcessBatchQuery(int BatchSize) : IStreamRequest<BatchResult>;

public class ProcessBatchHandler : IStreamRequestHandler<ProcessBatchQuery, BatchResult>
{
    private readonly IQueueService _queueService;

    public ProcessBatchHandler(IQueueService queueService)
        => _queueService = queueService;

    public async IAsyncEnumerable<BatchResult> Handle(
        ProcessBatchQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var batchNumber = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var items = await _queueService.DequeueAsync(request.BatchSize, cancellationToken);

            if (!items.Any())
                break;

            batchNumber++;
            var result = await ProcessBatchAsync(items, cancellationToken);

            yield return new BatchResult
            {
                BatchNumber = batchNumber,
                ItemsProcessed = result.Count,
                SuccessCount = result.SuccessCount,
                FailureCount = result.FailureCount
            };
        }
    }

    private Task<ProcessingResult> ProcessBatchAsync(
        IEnumerable<QueueItem> items,
        CancellationToken cancellationToken)
    {
        // Processing logic
        return Task.FromResult(new ProcessingResult());
    }
}
```

---

## Stream Pipeline Behaviors

Just like regular requests, streaming requests can have behaviors that wrap around them.

### IStreamPipelineBehavior Interface

```csharp
public interface IStreamPipelineBehavior<in TRequest, TResponse> where TRequest : notnull
{
    IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
```

### StreamHandlerDelegate

```csharp
public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<out TResponse>();
```

### Example: Stream Logging Behavior

```csharp
public class StreamLoggingBehavior<TRequest, TResponse>
    : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<StreamLoggingBehavior<TRequest, TResponse>> _logger;

    public StreamLoggingBehavior(ILogger<StreamLoggingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Starting stream for {RequestName}", requestName);

        var itemCount = 0;
        var stopwatch = Stopwatch.StartNew();

        await foreach (var item in next().WithCancellation(cancellationToken))
        {
            itemCount++;
            _logger.LogDebug("Yielding item {ItemCount} for {RequestName}", itemCount, requestName);
            yield return item;
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "Completed stream for {RequestName}. Items: {ItemCount}, Duration: {Duration}ms",
            requestName,
            itemCount,
            stopwatch.ElapsedMilliseconds);
    }
}
```

### Example: Stream Filtering Behavior

```csharp
public interface IFilterable
{
    bool ShouldInclude(object item);
}

public class StreamFilteringBehavior<TRequest, TResponse>
    : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in next().WithCancellation(cancellationToken))
        {
            if (request is IFilterable filterable && !filterable.ShouldInclude(item))
                continue;

            yield return item;
        }
    }
}
```

### Example: Stream Batching Behavior

```csharp
public class StreamBatchingBehavior<TRequest, TResponse>
    : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly int _batchSize;

    public StreamBatchingBehavior(int batchSize = 100)
        => _batchSize = batchSize;

    public async IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var batch = new List<TResponse>(_batchSize);

        await foreach (var item in next().WithCancellation(cancellationToken))
        {
            batch.Add(item);

            if (batch.Count >= _batchSize)
            {
                // Process batch
                foreach (var batchItem in batch)
                {
                    yield return batchItem;
                }
                batch.Clear();
            }
        }

        // Process remaining items
        foreach (var item in batch)
        {
            yield return item;
        }
    }
}
```

### Registering Stream Behaviors

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Register stream behaviors
    cfg.AddOpenStreamBehavior(typeof(StreamLoggingBehavior<,>));
    cfg.AddOpenStreamBehavior(typeof(StreamFilteringBehavior<,>));
});
```

---

## Cancellation Support

Always respect cancellation tokens in streaming handlers:

### Proper Cancellation Handling

```csharp
public class GetLogsStreamHandler : IStreamRequestHandler<GetLogsStreamQuery, LogEntry>
{
    private readonly ILogRepository _logRepository;

    public GetLogsStreamHandler(ILogRepository logRepository)
        => _logRepository = logRepository;

    public async IAsyncEnumerable<LogEntry> Handle(
        GetLogsStreamQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Method 1: Use WithCancellation
        await foreach (var log in _logRepository
            .GetLogsAsync(request.StartDate, request.EndDate)
            .WithCancellation(cancellationToken))
        {
            yield return log;
        }

        // Method 2: Check manually
        foreach (var log in await _logRepository.GetBatchAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return log;
        }
    }
}
```

### Cancellation with Cleanup

```csharp
public class StreamWithCleanupHandler
    : IStreamRequestHandler<StreamDataQuery, DataChunk>
{
    private readonly IDataSource _dataSource;
    private readonly ILogger<StreamWithCleanupHandler> _logger;

    public async IAsyncEnumerable<DataChunk> Handle(
        StreamDataQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        try
        {
            await foreach (var chunk in connection
                .StreamDataAsync(request.Query)
                .WithCancellation(cancellationToken))
            {
                yield return chunk;
            }
        }
        finally
        {
            // Cleanup happens even if cancelled
            await connection.DisposeAsync();
            _logger.LogInformation("Connection disposed");
        }
    }
}
```

---

## Advanced Patterns

### Throttling Stream Results

```csharp
public class ThrottledStreamHandler : IStreamRequestHandler<ThrottledQuery, DataItem>
{
    private readonly IDataRepository _repository;
    private readonly TimeSpan _delay;

    public ThrottledStreamHandler(IDataRepository repository, TimeSpan delay)
    {
        _repository = repository;
        _delay = delay;
    }

    public async IAsyncEnumerable<DataItem> Handle(
        ThrottledQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in _repository.GetDataAsync(cancellationToken))
        {
            yield return item;

            // Throttle the stream
            await Task.Delay(_delay, cancellationToken);
        }
    }
}
```

### Transforming Stream Items

```csharp
public class TransformStreamHandler
    : IStreamRequestHandler<GetUsersStreamQuery, UserDto>
{
    private readonly IUserRepository _repository;
    private readonly IMapper _mapper;

    public TransformStreamHandler(IUserRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async IAsyncEnumerable<UserDto> Handle(
        GetUsersStreamQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var user in _repository.StreamUsersAsync(cancellationToken))
        {
            // Transform each item before yielding
            var dto = _mapper.Map<UserDto>(user);
            yield return dto;
        }
    }
}
```

### Parallel Stream Processing

```csharp
public class ParallelStreamHandler
    : IStreamRequestHandler<ProcessItemsQuery, ProcessedItem>
{
    private readonly IItemRepository _repository;
    private readonly IProcessor _processor;

    public async IAsyncEnumerable<ProcessedItem> Handle(
        ProcessItemsQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<ProcessedItem>();

        // Producer: Read items and queue for processing
        var producerTask = Task.Run(async () =>
        {
            await foreach (var item in _repository.GetItemsAsync(cancellationToken))
            {
                await channel.Writer.WriteAsync(
                    await _processor.ProcessAsync(item, cancellationToken),
                    cancellationToken);
            }
            channel.Writer.Complete();
        }, cancellationToken);

        // Consumer: Yield processed items
        await foreach (var processedItem in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return processedItem;
        }

        await producerTask;
    }
}
```

---

## ASP.NET Core Integration

### Streaming to HTTP Response

```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("stream")]
    public async IAsyncEnumerable<Product> StreamProducts(
        [FromQuery] string category,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = new GetProductsStreamQuery(category);

        await foreach (var product in _mediator.CreateStream(query, cancellationToken))
        {
            yield return product;
        }
    }

    [HttpGet("stream/json-lines")]
    public async Task StreamProductsAsJsonLines(
        [FromQuery] string category,
        CancellationToken cancellationToken)
    {
        Response.ContentType = "application/x-ndjson";

        var query = new GetProductsStreamQuery(category);

        await foreach (var product in _mediator.CreateStream(query, cancellationToken))
        {
            var json = JsonSerializer.Serialize(product);
            await Response.WriteAsync(json + "\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}
```

### Server-Sent Events (SSE)

```csharp
[HttpGet("events")]
public async Task StreamEvents(CancellationToken cancellationToken)
{
    Response.Headers.Append("Content-Type", "text/event-stream");
    Response.Headers.Append("Cache-Control", "no-cache");
    Response.Headers.Append("Connection", "keep-alive");

    var query = new MonitorSystemMetricsQuery();

    await foreach (var metric in _mediator.CreateStream(query, cancellationToken))
    {
        var data = JsonSerializer.Serialize(metric);
        await Response.WriteAsync($"data: {data}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
```

---

## Best Practices

### Design Guidelines

1. **Use for large datasets:** Streaming is ideal when dealing with large or unbounded data
2. **Support cancellation:** Always handle `CancellationToken` properly
3. **Keep items independent:** Each yielded item should be processable independently
4. **Avoid buffering:** Don't accumulate items in memory - yield as you go
5. **Use EnumeratorCancellation:** Always use `[EnumeratorCancellation]` attribute

### Performance Considerations

1. **Yield early and often:** Don't wait to accumulate results
2. **Minimize allocations:** Reuse objects when possible
3. **Consider batching:** For very small items, consider batching for efficiency
4. **Monitor backpressure:** Be aware of consumer processing speed
5. **Use async all the way:** Don't mix sync and async operations

### Error Handling

```csharp
public async IAsyncEnumerable<Result> Handle(
    StreamQuery request,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    var errorCount = 0;

    await foreach (var item in _repository.StreamAsync(cancellationToken))
    {
        Result result;
        try
        {
            result = await ProcessItemAsync(item, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing item {ItemId}", item.Id);
            errorCount++;

            // Decide: continue or stop?
            if (errorCount > 10)
                throw;

            continue;
        }

        yield return result;
    }
}
```

---

## Testing Stream Handlers

### Unit Testing

```csharp
[Fact]
public async Task Handle_ReturnsStreamedProducts()
{
    // Arrange
    var repository = Substitute.For<IProductRepository>();
    var products = new[] { new Product { Id = 1 }, new Product { Id = 2 } };
    repository.StreamByCategoryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
        .Returns(products.ToAsyncEnumerable());

    var handler = new GetProductsStreamHandler(repository, NullLogger<GetProductsStreamHandler>.Instance);
    var query = new GetProductsStreamQuery("Electronics");

    // Act
    var results = new List<Product>();
    await foreach (var product in handler.Handle(query, CancellationToken.None))
    {
        results.Add(product);
    }

    // Assert
    Assert.Equal(2, results.Count);
    Assert.Equal(1, results[0].Id);
    Assert.Equal(2, results[1].Id);
}
```

### Testing with Cancellation

```csharp
[Fact]
public async Task Handle_RespectsCancellation()
{
    // Arrange
    var handler = new GetLogsStreamHandler(_repository);
    var query = new GetLogsStreamQuery(DateTime.Now, DateTime.Now);
    using var cts = new CancellationTokenSource();

    // Act & Assert
    var count = 0;
    await Assert.ThrowsAsync<OperationCanceledException>(async () =>
    {
        await foreach (var log in handler.Handle(query, cts.Token))
        {
            count++;
            if (count == 5)
                cts.Cancel(); // Cancel after 5 items
        }
    });

    Assert.Equal(5, count);
}
```

---

## Common Pitfalls

1. **Not using EnumeratorCancellation:** Cancellation token won't flow properly without it

2. **Buffering results:** Defeats the purpose of streaming - yield items as they arrive

3. **Ignoring cancellation:** Always check cancellation token in long-running streams

4. **Expensive operations per item:** Keep per-item processing lightweight

5. **Mixing sync and async:** Use async throughout the streaming pipeline

---

## Comparison: Streaming vs Regular Requests

| Feature | Streaming (`IStreamRequest`) | Regular (`IRequest`) |
|---------|------------------------------|---------------------|
| Return type | `IAsyncEnumerable<T>` | `Task<T>` |
| Memory usage | Constant (streaming) | Linear (all at once) |
| First item latency | Low | High |
| Use case | Large datasets, real-time | Small results, complete data |
| Cancellation | Per item | Entire request |
| Complexity | Higher | Lower |

---

## Next Steps

- **[Pipeline Behaviors](./04-behaviors.md)** - Add cross-cutting concerns
- **[Configuration](./05-configuration.md)** - Configure stream behaviors
- **[Exception Handling](./06-exception-handling.md)** - Handle errors in streams
