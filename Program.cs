using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// InjectMemoryRepository(builder.Services);
InjectDataContextRepository(builder.Services, builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();

// app.UseMiddleware<LoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

MapRoutes<Product>("Products");
MapRoutes<Category>("Categories");

app.MapGet("/error", _ => throw new DivideByZeroException());

app.Run();


void InjectMemoryRepository(IServiceCollection services)
{
    services.AddSingleton<IRepository<Product>>(new MemoryRepository<Product>());
    services.AddSingleton<IRepository<Category>>(new MemoryRepository<Category>());
}

void InjectDataContextRepository(IServiceCollection services, ConfigurationManager configuration)
{
    var options = new DbContextOptionsBuilder<DataContext>()
        .UseSqlite(configuration.GetConnectionString("(default)"))
        .Options;

    var dataContext = new DataContext(options);
    dataContext.Database.EnsureCreated();

    services.AddScoped<DataContext>(_ => dataContext);
    services.AddScoped<IRepository<Product>>(_ => new DataContextRepository<Product>(dataContext));
    services.AddScoped<IRepository<Category>>(_ => new DataContextRepository<Category>(dataContext));
}


void MapRoutes<T>(string tag)
    where T : class, IHasId
{
    var prefix = $"/{tag}";
    var group = app.MapGroup(prefix).WithTags(tag);

    group.MapGet("/", (IRepository<T> repository) => repository.GetAll());

    group.MapGet("/{id}", (int id, IRepository<T> repository) => repository.Get(id))
        .Produces<T>(200)
        .Produces(404);

    group.MapPost("/", (T product, IRepository<T> repository) => repository.Add(product));

    group.MapDelete("/{id}", (int id, IRepository<T> repository) => repository.Delete(id));
}

interface IHasId
{
    int Id { get; set; }
}

record Category : IHasId
{
	public Category()
	{
		Products = new List<Product>();
	}

	public int Id { get; set; }

	required public string Name { get; set; }

	public ICollection<Product> Products { get; set; }
}

record Product : IHasId
{
	public Product()
	{
		Categories = new List<Category>();
	}

	public int Id { get; set; }
	
	required public string Title { get; set; }
	
	public decimal Price { get; set; }

	public decimal DiscountedPrice { get; set; }

	required public string Description { get; set; }

	required public string Image { get; set; }

	public ICollection<Category> Categories { get; set; }
}

interface IRepository<T>
    where T : class, IHasId
{
    Task Add(T item);

    Task Delete(int id);

    IAsyncEnumerable<T> GetAll();

    Task<T?> Get(int id);
}

class MemoryRepository<T> : IRepository<T>
    where T : class, IHasId
{
    readonly Dictionary<int, T> dict = new();

    public Task Add(T item)
    {
        dict[item.Id] = item;

        return Task.CompletedTask;
    }

    public Task<T?> Get(int id)
    {
        return Task.FromResult(dict.TryGetValue(id, out var item) ? item : null);
    }

    public async IAsyncEnumerable<T> GetAll()
    {
        foreach (var item in dict.Values) yield return item;
    }

    public Task Delete(int id)
    {
        dict.Remove(id);

        return Task.CompletedTask;
    }
}

class DataContext : DbContext
{
    public DataContext(DbContextOptions<DataContext> options) 
        : base(options)
    { }

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Category> Categories => Set<Category>();
}

class DataContextRepository<T> : IRepository<T>
    where T : class, IHasId
{
    readonly DbContext _dataContext;

    readonly DbSet<T> _entities;

    public DataContextRepository(DbContext dataContext)
    {
        ArgumentNullException.ThrowIfNull(dataContext);

        _dataContext = dataContext;
        _entities = _dataContext.Set<T>();
    }

    public async Task Add(T entity)
    {
        await _entities.AddAsync(entity);
        await _dataContext.SaveChangesAsync();
    }

    public async Task Delete(int id)
    {
        var entity = await Get(id);
        if (entity != null)
        {
            _dataContext.Remove(entity);
            await _dataContext.SaveChangesAsync();
        }
    }

    public async Task<T?> Get(int id)
    {
        return await _dataContext.FindAsync<T>(id);
    }

    public IAsyncEnumerable<T> GetAll()
    {
        return _entities.AsAsyncEnumerable();
    }
}

class LoggingMiddleware
{
    private readonly RequestDelegate _next;

    private readonly ILoggerFactory _loggerFactory;

    public LoggingMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
    {
        _next = next;
        _loggerFactory = loggerFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var logger = _loggerFactory.CreateLogger<LoggingMiddleware>();

        try
        {
            logger.LogInformation("Path: {0}, QueryString: {1}", 
                context.Request.Path,
                context.Request.QueryString);    
            
            await _next(context);
        }
        catch (Exception e)
        {
            logger.LogError(e, "");    
        }
    }
}