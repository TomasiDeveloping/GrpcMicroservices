using AutoMapper;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ShoppingCartGrpc.Data;
using ShoppingCartGrpc.Models;
using ShoppingCartGrpc.Protos;

namespace ShoppingCartGrpc.Services
{
    [Authorize]
    public class ShoppingCartService : ShoppingCartProtoService.ShoppingCartProtoServiceBase
    {
        private readonly ILogger<ShoppingCartService> _logger;
        private readonly ShoppingCartContext _shoppingCartContext;
        private readonly IMapper _mapper;
        private readonly DiscountService _discountService;

        public ShoppingCartService(ILogger<ShoppingCartService> logger, ShoppingCartContext shoppingCartContext, IMapper mapper, DiscountService discountService)
        {
            _logger = logger ?? throw new ArgumentException(nameof(logger));
            _shoppingCartContext = shoppingCartContext ?? throw new ArgumentException(nameof(shoppingCartContext));
            _mapper = mapper ?? throw new ArgumentException(nameof(mapper));
            _discountService = discountService ?? throw new ArgumentException(nameof(discountService));
        }

        public override async Task<ShoppingCartModel> GetShoppingCart(GetShoppingCartRequest request, ServerCallContext context)
        {
            var shoppingCart =
                await _shoppingCartContext.ShoppingCarts.FirstOrDefaultAsync(s => s.UserName == request.Username);

            if (shoppingCart == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound,
                    $"ShoppingCart with UserName = {request.Username} is not found"));
            }

            var shoppingCartModel = _mapper.Map<ShoppingCartModel>(shoppingCart);
            _logger.LogInformation("ShoppingCart with Username {userName}", request.Username);
            return shoppingCartModel;
        }

        public override async Task<ShoppingCartModel> CreateShoppingCart(ShoppingCartModel request, ServerCallContext context)
        {
            var shoppingCart = _mapper.Map<ShoppingCart>(request);
            var isExist = await _shoppingCartContext.ShoppingCarts.AnyAsync(s => s.UserName == shoppingCart.UserName);
            if (isExist)
            {
                _logger.LogInformation("Invalid UserName for ShoppingCart creation. UserName : {userName}", request.Username);
                throw new RpcException(new Status(StatusCode.NotFound,
                    $"ShoppingCart with Username = {request.Username} is already exist."));
            }

            await _shoppingCartContext.ShoppingCarts.AddAsync(shoppingCart);
            await _shoppingCartContext.SaveChangesAsync();

            _logger.LogInformation("ShoppingCart is successfully created. UserName : {userName}", shoppingCart.UserName);

            return _mapper.Map<ShoppingCartModel>(shoppingCart);
        }

        [AllowAnonymous]
        public override async Task<RemoveItemIntoShoppingCartResponse> RemoveItemIntoShoppingCart(RemoveItemIntoShoppingCartRequest request, ServerCallContext context)
        {
            // Get sc if exist or not
            var shoppingCart =
                await _shoppingCartContext.ShoppingCarts.FirstOrDefaultAsync(s => s.UserName == request.Username);
            if (shoppingCart == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound,
                    $"ShoppingCart with UserName = {request.Username} is not exist"));
            }
            // Check item if exist in sc or not
            var removeCartItem =
                shoppingCart.Items.FirstOrDefault(i => i.ProductId == request.RemoveCartItem.ProductId);
            if (removeCartItem == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound,
                    $"CartItem with ProductId = {request.RemoveCartItem.ProductId} is not found"));
            }
            // Remove item in sc db
            shoppingCart.Items.Remove(removeCartItem);
            var removeCount = await _shoppingCartContext.SaveChangesAsync();

            var response = new RemoveItemIntoShoppingCartResponse()
            {
                Success = removeCount > 0
            };

            return response;
        }

        [AllowAnonymous]
        public override async Task<AddItemIntoShoppingCartResponse> AddItemIntoShoppingCart(IAsyncStreamReader<AddItemIntoShoppingCartRequest> requestStream, ServerCallContext context)
        {
            while (await requestStream.MoveNext())
            {
                // Get sc if exist or not
                var shoppingCart =
                    await _shoppingCartContext.ShoppingCarts.FirstOrDefaultAsync(s =>
                        s.UserName == requestStream.Current.Username);
                if (shoppingCart == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound,
                        $"ShoppingCart with UserName = {requestStream.Current.Username} is not found"));
                }
                // Check the item if exist in sc or not
                var newAddedCartItem = _mapper.Map<ShoppingCartItem>(requestStream.Current.NewCartItem);
                var cartItem = shoppingCart.Items.FirstOrDefault(i => i.ProductId == newAddedCartItem.ProductId);
                // If item is exist +1 quantity
                if (cartItem != null)
                {
                    cartItem.Quantity++;

                }
                // If item is nor exist add new item in sc
                else
                {
                    // grpc call discount service -- check discount and calculate item last price
                    var discount = await _discountService.GetDiscount(requestStream.Current.DiscountCode);
                    
                    newAddedCartItem.Price -= discount.Amount;

                    shoppingCart.Items.Add(newAddedCartItem);
                }
            }

            var insertCount = await _shoppingCartContext.SaveChangesAsync();
            var response = new AddItemIntoShoppingCartResponse
            {
                Success = insertCount > 0,
                InsertCount = insertCount
            };

            return response;

        }
    }
}
