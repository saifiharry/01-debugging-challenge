using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System.Globalization;

namespace EcommerceTests.Integration
{
    [TestFixture]
    public class CheckoutTests : PageTest
    {
        public override BrowserContextOptions ContextOptions => new()
        {
            WaitUntil = WaitUntilState.NetworkIdle
        };

        [Test]
        public async Task TestCheckoutProcessWithDiscount()
        {
            // Navigate to product page
            await Page.GotoAsync("https://staging.example-shop.com/products/laptop-pro");

            var cartResponseListener = Page.WaitForResponseAsync(response => response.Url.Contains("/api/v1/cart/add") && response.Request.Method == "POST");

            // Add to cart
            var addToCartBtn = Page.Locator("#add-to-cart");
            await addToCartBtn.ClickAsync();

            var cartResponse = await cartResponseListener;
            Assert.That(cartResponse.Status, Is.EqualTo(200));

            await Expect(Page.Locator(".cart-count")).ToBeVisibleAsync();

            // Navigate to cart page
            await Page.GotoAsync("https://staging.example-shop.com/cart");

            var priceElement = Page.Locator(".cart-total");
            await Expect(priceElement).ToBeVisibleAsync();

            var priceText = await priceElement.InnerTextAsync();
            var originalPrice = decimal.Parse(priceText.Replace("$", "").Replace(",", "").Trim());

            Console.WriteLine($"Original price: {originalPrice}");

            var checkoutBtn = Page.Locator("#checkout-button");
            await Expect(checkoutBtn).ToBeVisibleAsync();

            await checkoutBtn.ClickAsync();

            var emailInput= Page.Locator("#email");
            await Expect(emailInput).ToBeVisibleAsync();

            //await Page.Locator("#first-name").FillAsync("John");
            //await Page.Locator("#last-name").FillAsync("Smith");
            //await Page.Locator("#city").FillAsync("New York");
            //await Page.Locator("#postal-code").FillAsync("10001");

            // In Provided Screenshot - It Don't have separate Locator for First Name, Last Name, City and Postal Code

            await emailInput.FillAsync("john.smith@example.com");
            await Page.Locator("#address").FillAsync("123 Main Street, New York, NY 10001");
            await Page.Locator("#full-name").FillAsync("John Smith");

            
            var pricingCalculation =
            Page.WaitForResponseAsync(response =>
                response.Url.Contains("/api/v1/pricing/calculate")
                && response.Request.Method == "POST",new PageWaitForResponseOptions{
                     Timeout = 15000
                 });

            var discountInput = Page.Locator("#discount-code");
            await discountInput.FillAsync("SAVE20");

            var applyBtn = Page.Locator("#apply-discount");
            await applyBtn.ClickAsync();

            var pricingResponse = await pricingCalculation;
            Assert.That(pricingResponse.Ok,Is.True,$"pricing/calculate request failed. HTTP status: {pricingResponse.Status}");

            await Expect(Page.Locator(".discount-applied"))
                .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions{
                    Timeout = 15000
                });

            
            var finalPriceElement = Page.Locator(".final-price");
            var finalPriceText = await finalPriceElement.InnerTextAsync();
            var finalPrice = decimal.Parse(finalPriceText.Replace("$", "").Replace(",", "").Trim());

            Console.WriteLine($"Final price: {finalPrice}");

            var expectedPrice = decimal.Round(originalPrice * 0.80m,2);

            Assert.AreEqual(expectedPrice, finalPrice,
                $"Expected price {expectedPrice} but got {finalPrice}");

            await Expect(Page.Locator(".discount-badge")).ToHaveTextAsync("-20%");

            await Page.Locator("#payment-method-card").ClickAsync();
            await Page.Locator("#card-number").FillAsync("4111111111111111");
            //await Page.Locator("#card-expiry").FillAsync("12/25");

            //Update the Card Expiry
            await Page.Locator("#card-expiry").FillAsync("12/30"); 
            await Page.Locator("#card-cvc").FillAsync("123");

            var placeOrderBtn = Page.Locator("#place-order");
            await Expect(placeOrderBtn).ToBeEnabledAsync();
            await placeOrderBtn.ClickAsync();

            await Page.WaitForURLAsync("**/order-confirmation", new PageWaitForURLOptions
            {
                Timeout = 15000
            });

            await Expect(Page.Locator(".success-message"))
            .ToContainTextAsync("Thank you for your order");
        }
    }   
}
