using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("(default)");
var options = new DbContextOptionsBuilder<DataContext>()
	.UseSqlite(connectionString)
	.Options;

using var context = new DataContext(options);


var app = builder.Build();

app.MapGet("/categories", () => context.Categories.ToListAsync());

app.MapGet("/categories/{Id:int}", async (int id) => 
{
    var category = await context.Categories.FindAsync(id);
    if (category == null) return Results.NotFound();

    return Results.Ok(category);
});

app.MapDelete("/categories/{Id:int}", async (int id) =>
{
    var category = await context.Categories.FindAsync(id);
    if (category == null) return Results.NotFound();

    context.Categories.Remove(category);
    await context.SaveChangesAsync();

    return Results.Ok(category);
});

app.Run();


class DataContext : DbContext
{
	public DataContext(DbContextOptions<DataContext> options)
		: base(options)
	{ }

	public DbSet<Product> Products => Set<Product>();

	public DbSet<Category> Categories => Set<Category>();
}

record Category
{
	public Category()
	{
		Products = new List<Product>();
	}

	public int Id { get; set; }

	required public string Name { get; set; }

	public ICollection<Product> Products { get; set; }
}

record Product
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