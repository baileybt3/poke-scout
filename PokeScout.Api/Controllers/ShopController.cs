using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PokeScout.Api.Dtos;
using PokeScout.Api.Models;
using PokeScout.Api.Services;
using Stripe.Checkout;

namespace PokeScout.Api.Controllers
{
    [ApiController]
    [Route("api/shop")]
    public class ShopController : ControllerBase
    {
        private readonly StripeOptions _stripeOptions;

        public ShopController(IOptions<StripeOptions> stripeOptions)
        {
            _stripeOptions = stripeOptions.Value;
        }

        [HttpGet]
        public ActionResult<List<ShopCatalogItemDto>> GetAll()
        {
            return Ok(ShopCatalogSeed.GetItems());
        }

        [HttpPost("checkout-session")]
        public ActionResult<CreateCheckoutSessionResponse> CreateCheckoutSession(
            [FromBody] CreateCheckoutSessionRequest request)
        {
            var item = ShopCatalogSeed.GetItems()
                .FirstOrDefault(x => x.Id == request.ProductId);

            if (item is null)
            {
                return NotFound("Product not found.");
            }

            if (!item.IsStripeReady)
            {
                return BadRequest("This product is not ready for Stripe checkout yet.");
            }

            var domain = _stripeOptions.FrontendBaseUrl.TrimEnd('/');

            var options = new SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = $"{domain}/shop/success?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{domain}/shop",
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "usd",
                            UnitAmount = (long)(item.Price * 100),
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = item.Title,
                                Description = item.Description
                            }
                        }
                    }
                }
            };

            var service = new SessionService();
            var session = service.Create(options);

            return Ok(new CreateCheckoutSessionResponse
            {
                Url = session.Url
            });
        }
    }
}