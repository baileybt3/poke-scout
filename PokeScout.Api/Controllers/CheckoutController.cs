using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PokeScout.Api.Dtos;
using PokeScout.Api.Models;
using PokeScout.Api.Services;
using Stripe.Checkout;

namespace PokeScout.Api.Controllers
{
    [ApiController]
    [Route("api/checkout")]
    public class CheckoutController : ControllerBase
    {
        private readonly StripeOptions _stripeOptions;

        public CheckoutController(IOptions<StripeOptions> stripeOptions)
        {
            _stripeOptions = stripeOptions.Value;
        }

        [HttpPost("create-session")]
        public async Task<ActionResult<CreateCheckoutSessionResponse>> CreateSession(
            [FromBody] CreateCheckoutSessionRequest request)
        {
            var product = ShopCatalogSeed
                .GetItems()
                .FirstOrDefault(x => x.Id == request.ProductId);

            if (product is null)
            {
                return NotFound("Product not found.");
            }

            if (!product.IsStripeReady)
            {
                return BadRequest("This product is not Stripe-ready yet.");
            }

            var options = new SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = $"{_stripeOptions.FrontendBaseUrl}/catalog?checkout=success&session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{_stripeOptions.FrontendBaseUrl}/catalog?checkout=cancel",
                ClientReferenceId = product.Id,
                Metadata = new Dictionary<string, string>
                {
                    ["product_id"] = product.Id,
                    ["product_title"] = product.Title
                },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "usd",
                            UnitAmount = (long)(product.Price * 100m),
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = product.Title,
                                Description = product.Description
                            }
                        }
                    }
                }
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            return Ok(new CreateCheckoutSessionResponse
            {
                Url = session.Url
            });
        }
    }
}