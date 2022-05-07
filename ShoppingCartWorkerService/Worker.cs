using Grpc.Core;
using Grpc.Net.Client;
using IdentityModel.Client;
using ProductGrpc.Protos;
using ShoppingCartGrpc.Protos;

namespace ShoppingCartWorkerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration ?? throw new ArgumentException(nameof(configuration));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Waiting for server is running...");
            Thread.Sleep(2000);


            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                // Create SC if not exist
                using var scChannel =
                    GrpcChannel.ForAddress(_configuration.GetValue<string>("WorkerService:ShoppingCartServerUrl"));
                var scClient = new ShoppingCartProtoService.ShoppingCartProtoServiceClient(scChannel);

                // Get Token from IS4
                var token = await GetTokenFromIS4();

                await GetOrCreateShoppingCartAsync(scClient, token);
                using var scClientStream = scClient.AddItemIntoShoppingCart();

                // Retrieve products from product grpc with server stream
                using var productChannel =
                    GrpcChannel.ForAddress(_configuration.GetValue<string>("WorkerService:ProductServerUrl"));
                var productClient = new ProductProtoService.ProductProtoServiceClient(productChannel);
                _logger.LogInformation("GetAllProducts started...");
                using var clientData = productClient.GetAllProducts(new GetAllProductsRequest());
                await foreach (var responseData in clientData.ResponseStream.ReadAllAsync(cancellationToken: stoppingToken))
                {
                    _logger.LogInformation("GetAllProducts Stream Response: {responsData}", responseData);
                    // Add sc items into SC with client stream
                    var addNewScItem = new AddItemIntoShoppingCartRequest()
                    {
                        Username = _configuration.GetValue<string>("WorkerService:UserName"),
                        DiscountCode = "CODE_100",
                        NewCartItem = new ShoppingCartItemModel()
                        {
                            ProductId = responseData.ProductId,
                            Productname = responseData.Name,
                            Price = responseData.Price,
                            Color = "Black",
                            Quantity = 1
                        }
                    };

                    await scClientStream.RequestStream.WriteAsync(addNewScItem, stoppingToken);
                    _logger.LogInformation("ShoppingCart Client Stream Added New Item : {addNewScItem}", addNewScItem);
                }

                await scClientStream.RequestStream.CompleteAsync();

                var addItemIntoShoppingCartResponse = await scClientStream;
                _logger.LogInformation(
                    "AddItemIntoShoppingCart Client Stream Response : {addItemIntoShoppingCartResponse}",
                    addItemIntoShoppingCartResponse);

                await Task.Delay(_configuration.GetValue<int>("WorkerService:TaskInterval"), stoppingToken);
            }
        }

        private async Task<string> GetTokenFromIS4()
        {
            // Discover endpoints from metadata
            var client = new HttpClient();
            var disco = await client.GetDiscoveryDocumentAsync(
                _configuration.GetValue<string>("WorkerService:IdentityServerUrl"));
            if (disco.IsError)
            {
                Console.WriteLine(disco.Error);
                return string.Empty;
            }
            
            // Request token
            var tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest()
            {
                Address = disco.TokenEndpoint,
                ClientId = "ShoppingCartClient",
                ClientSecret = "secret",
                Scope = "ShoppingCartAPI"
            });

            if (tokenResponse.IsError)
            {
                Console.WriteLine(tokenResponse.Error);
                return string.Empty;
            }

            return tokenResponse.AccessToken;
        }

        private async Task GetOrCreateShoppingCartAsync(
            ShoppingCartProtoService.ShoppingCartProtoServiceClient scClient, string token)
        {
            // try to get sc
            // create sc
            ShoppingCartModel shoppingCartModel;
            try
            {
                _logger.LogInformation("GetShoppingCartAsync started...");

                var headers = new Metadata();
                headers.Add("Authorization", $"Bearer {token}");

                shoppingCartModel = await scClient.GetShoppingCartAsync(new GetShoppingCartRequest()
                    {Username = _configuration.GetValue<string>("WorkerService:UserName")}, headers);
                _logger.LogInformation("GetShoppingCartAsync Response: {shoppingCartModel}", shoppingCartModel);
            }
            catch (RpcException exception)
            {
                if (exception.StatusCode == StatusCode.NotFound)
                {
                    _logger.LogInformation("CreateShoppingCartAsync started..");
                    shoppingCartModel = await scClient.CreateShoppingCartAsync(new ShoppingCartModel()
                        {Username = _configuration.GetValue<string>("WorkerService:UserName")});
                    _logger.LogInformation("CreateShoppingCartAsync Response: {shoppingCartModel}", shoppingCartModel);
                }
                else
                {
                    throw;
                }
            }
        }
    }
}