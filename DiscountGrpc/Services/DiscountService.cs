using DiscountGrpc.Data;
using DiscountGrpc.Protos;
using Grpc.Core;

namespace DiscountGrpc.Services
{
    public class DiscountService : DiscountProtoService.DiscountProtoServiceBase
    {
        private readonly ILogger<DiscountService> _logger;

        public DiscountService(ILogger<DiscountService> logger)
        {
            _logger = logger ?? throw new ArgumentException(nameof(logger));
        }
        public override Task<DiscountModel> GetDiscount(GetDiscountRequest request, ServerCallContext context)
        {
            var discount = DiscountContext.Discounts.FirstOrDefault(d => d.Code == request.DiscountCode);

            _logger.LogInformation("Discount is operated with the {discountCode} code and the amount is : {discountAmount}", discount.Code, discount.Amount);

            var response = new DiscountModel
            {
                Amount = discount.Amount,
                Code = discount.Code,
                DiscountId = discount.DiscountId
            };

            return Task.FromResult(response);

        }
    }
}
