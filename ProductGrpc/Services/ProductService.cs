using AutoMapper;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using ProductGrpc.Data;
using ProductGrpc.Models;
using ProductGrpc.Protos;


namespace ProductGrpc.Services
{
    public class ProductService : ProductProtoService.ProductProtoServiceBase
    {
        private readonly ILogger<ProductService> _logger;
        private readonly ProductContext _productContext;
        private readonly IMapper _mapper;

        public ProductService(ILogger<ProductService> logger, ProductContext productContext, IMapper mapper)
        {
            _logger = logger ?? throw new AbandonedMutexException(nameof(productContext));
            _productContext = productContext ?? throw new AbandonedMutexException(nameof(logger));
            _mapper = mapper ?? throw new AggregateException(nameof(mapper));
        }

        public override async Task<ProductModel> GetProduct(GetProductRequest request, ServerCallContext context)
        {
            var product = await _productContext.Products.FindAsync(request.ProductId);
            if (product == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound,
                    $"Product with ID={request.ProductId} is not found"));
            }

            var productModel = _mapper.Map<ProductModel>(product);
            return productModel;
        }

        public override async Task GetAllProducts(GetAllProductsRequest request, IServerStreamWriter<ProductModel> responseStream, ServerCallContext context)
        {
            var products = await _productContext.Products.ToListAsync();

            foreach (var productModel in products.Select(product => _mapper.Map<ProductModel>(product)))
            {
                await responseStream.WriteAsync(productModel);
            }
        }

        public override async Task<ProductModel> AddProduct(AddProductRequest request, ServerCallContext context)
        {
            var product = _mapper.Map<Product>(request.Product);

            await _productContext.Products.AddAsync(product);
            await _productContext.SaveChangesAsync();

            _logger.LogInformation("Product successfully added : {productId}_{productName}", product.ProductId, product.Name);

            var productModel = _mapper.Map<ProductModel>(product);
            return productModel;
        }

        public override async Task<ProductModel> UpdateProduct(UpdateProductRequest request, ServerCallContext context)
        {
            var product = _mapper.Map<Product>(request.Product);

            var isExist = await _productContext.Products.AnyAsync(p => p.ProductId == product.ProductId);
            if (!isExist)
            {
                throw new RpcException(new Status(StatusCode.NotFound,
                    $"Product with ID={request.Product.ProductId} is not found"));
            }

            _productContext.Entry(product).State = EntityState.Modified;
            try
            {
                await _productContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                throw new RpcException(new Status(StatusCode.Internal, e.ToString()));
            }

            var productModel = _mapper.Map<ProductModel>(product);
            return productModel;

        }

        public override async Task<DeleteProductResponse> DeleteProduct(DeleteProductRequest request, ServerCallContext context)
        {
            var product = await _productContext.Products.FindAsync(request.ProductId);

            if (product == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound,
                    $"Product with ID={request.ProductId} is not found"));
            }

            _productContext.Products.Remove(product);
            var deleteCount = await _productContext.SaveChangesAsync();
            var response = new DeleteProductResponse
            {
                Success = deleteCount > 0
            };

            return response;
        }

        public override async Task<InsertBulkProductResponse> InsertBulkProduct(IAsyncStreamReader<ProductModel> requestStream, ServerCallContext context)
        {
            while (await requestStream.MoveNext())
            {
                var product = _mapper.Map<Product>(requestStream.Current);
                _productContext.Products.Add(product);
            }

            var insertCount = await _productContext.SaveChangesAsync();

            var response = new InsertBulkProductResponse
            {
                Success = insertCount > 0,
                InsertCount = insertCount
            };
            return response;
        }
    }

}
