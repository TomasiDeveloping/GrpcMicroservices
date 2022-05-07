using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using ProductGrpc.Protos;

Console.WriteLine("Waiting for server is running...");
Thread.Sleep(2000);

using var channel = GrpcChannel.ForAddress("https://localhost:7138");
var client = new ProductProtoService.ProductProtoServiceClient(channel);

// GetProductAsync
await GetProductAsync(client);
// GetAllProducts
await GetAllProducts(client);
// AddProduct
await AddProductAsync(client);
// UpdateProduct
await UpdateProductAsync(client);
// DeleteProduct
await DeleteProductAsync(client);
// InsertBulk
await InsertBulkProduct(client);
await GetAllProducts(client);


Console.ReadLine();


static async Task GetProductAsync(ProductProtoService.ProductProtoServiceClient client)
{
    Console.WriteLine("GetProductAsync started..");
    var response = await client.GetProductAsync(
        new GetProductRequest { ProductId = 1 }
    );
    Console.WriteLine("GetProductAsync Response :" + response);
}

static async Task GetAllProducts(ProductProtoService.ProductProtoServiceClient client)
{
    //Console.WriteLine("GetAllProduct started..");
    //using (var clientData = client.GetAllProducts(new GetAllProductsRequest()))
    //{
    //    while (await clientData.ResponseStream.MoveNext(new CancellationToken()))
    //    {
    //        var currentProduct = clientData.ResponseStream.Current;
    //        Console.WriteLine(currentProduct);
    //    }
    //}

    // GetAllProducts with C# 9
    Console.WriteLine("GetAllProducts with C# 9 started..");
    using var clientData = client.GetAllProducts(new GetAllProductsRequest());
    await foreach (var responseData in clientData.ResponseStream.ReadAllAsync())
    {
        Console.WriteLine(responseData);
    }
}

static async Task AddProductAsync(ProductProtoService.ProductProtoServiceClient client)
{
    Console.WriteLine("AddProductAsync started..");
    var addProductResponse = await client.AddProductAsync(
        new AddProductRequest()
        {
            Product = new ProductModel()
            {
                Name = "Red",
                Description = "New Red Phone Mi10T",
                Price = 699,
                Status = ProductStatus.Instock,
                CreatedTime = Timestamp.FromDateTime(DateTime.UtcNow)
            }
        }
    );
    Console.WriteLine("AddProduct Response: " + addProductResponse);
}

static async Task UpdateProductAsync(ProductProtoService.ProductProtoServiceClient client)
{
    Console.WriteLine("UpdateProductAsync started..");
    var updateProductResponse = await client.UpdateProductAsync(
        new UpdateProductRequest()
        {
            Product = new ProductModel()
            {
                ProductId = 1,
                Name = "Red",
                Description = "New Red Phone Mi10T",
                Price = 699,
                Status = ProductStatus.Instock,
                CreatedTime = Timestamp.FromDateTime(DateTime.UtcNow)
            }
        }
        );
    Console.WriteLine("UpdateProductAsync Response: " + updateProductResponse);
}

static async Task DeleteProductAsync(ProductProtoService.ProductProtoServiceClient client)
{
    Console.WriteLine("DeleteProductAsync started..");
    var deleteProductResponse = await client.DeleteProductAsync(
        new DeleteProductRequest()
        {
            ProductId = 1
        }
        );
    Console.WriteLine("DeleteProductAsync Response: " + deleteProductResponse);
}

static async Task InsertBulkProduct(ProductProtoService.ProductProtoServiceClient client)
{
    Console.WriteLine("InsertBulkProduct started..");

    using var clientBulk = client.InsertBulkProduct();

    for (var i = 0; i < 3; i++)
    {
        var productModel = new ProductModel()
        {
            Name = $"Product{i}",
            Description = "Bulk inserted product",
            Status = ProductStatus.Instock,
            CreatedTime = Timestamp.FromDateTime(DateTime.UtcNow)
        };

        await clientBulk.RequestStream.WriteAsync(productModel);
    }

    await clientBulk.RequestStream.CompleteAsync();

    var responseBulk = await clientBulk;
    Console.WriteLine($"Status: {responseBulk.Success}. Insert Count: {responseBulk.InsertCount}");

}