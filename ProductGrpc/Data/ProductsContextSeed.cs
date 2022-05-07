using ProductGrpc.Models;

namespace ProductGrpc.Data
{
    public class ProductsContextSeed
    {
        public static void SeedAsync(ProductContext productContext)
        {
            if (productContext.Products.Any()) return;
            var products = new List<Product>
            {
                new()
                {
                    ProductId = 1,
                    Name = "Mi10T",
                    Description = "New Xiaomi Phone Mi10T",
                    Price = 699,
                    Status = ProductStatus.INSTOCK,
                    CreatedTime = DateTime.UtcNow
                },
                new()
                {
                    ProductId = 2,
                    Name = "P40",
                    Description = "New Huawei Phone P40",
                    Price = 899,
                    Status = ProductStatus.INSTOCK,
                    CreatedTime = DateTime.UtcNow
                },
                new()
                {
                    ProductId = 3,
                    Name = "A50",
                    Description = "New Samsung Phone A50",
                    Price = 399,
                    Status = ProductStatus.INSTOCK,
                    CreatedTime = DateTime.UtcNow
                }
            };
            productContext.Products.AddRange(products);
            productContext.SaveChanges();
        }
    }
}
