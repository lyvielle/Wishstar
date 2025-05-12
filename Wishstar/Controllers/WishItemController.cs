using Microsoft.AspNetCore.Mvc;
using Wishstar.Models;
using Wishstar.Extensions;

namespace Wishstar.Controllers {
    [ApiController]
    [Route("/api/wishes")]
    public class WishItemController(ILogger<WishItemController> logger) : ControllerBase {
        private readonly ILogger<WishItemController> _Logger = logger;

        public record class AddWishRequest(WishItem WishItem);
        public record class UpdateWishRequest(WishItem WishItem);
        public record class DeleteWishRequest(int WishId);

        [HttpPost]
        [Route("add")]
        public IActionResult Add([FromForm] AddWishRequest request) {
            try {
                if (!Request.TryValidateLogin(out _, out _)) {
                    return Unauthorized("You must be logged in to add a wish item");
                }

                if (request.WishItem is null) {
                    return BadRequest("Wish item is required");
                }

                var existing = WishDatabase.Load().GetWishes().FirstOrDefault(x => x.WishId == request.WishItem.WishId);
                if (existing != null) {
                    return BadRequest("Wish item already exists");
                }

                WishDatabase.Load().AddWish(request.WishItem);

                return Ok();
            } catch (Exception ex) {
                _Logger.LogError(ex, "Failed to add wish item");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to add wish item");
            }
        }

        [HttpPost]
        [Route("update")]
        public IActionResult Update([FromForm] UpdateWishRequest request) {
            try {
                if (!Request.TryValidateLogin(out _, out _)) {
                    return Unauthorized("You must be logged in to update a wish item");
                }

                if (request.WishItem is null) {
                    return BadRequest("Wish item is required");
                }

                var oldWish = WishDatabase.Load().GetWishes().FirstOrDefault(x => x.WishId == request.WishItem.WishId);
                if (oldWish == null) {
                    return BadRequest("Wish item does not exist");
                }

                WishDatabase.Load().UpdateWish(oldWish, request.WishItem);

                return Ok();
            } catch (Exception ex) {
                _Logger.LogError(ex, "Failed to update wish item");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to update wish item");
            }
        }

        [HttpPost]
        [Route("delete")]
        public IActionResult Delete([FromForm] DeleteWishRequest request) {
            try {
                if (!Request.TryValidateLogin(out _, out _)) {
                    return Unauthorized("You must be logged in to delete a wish item");
                }

                var wish = WishDatabase.Load().GetWishes().FirstOrDefault(x => x.WishId == request.WishId);
                if (wish == null) {
                    return BadRequest("Wish item does not exist");
                }

                WishDatabase.Load().DeleteWish(wish);

                return Ok();
            } catch (Exception ex) {
                _Logger.LogError(ex, "Failed to delete wish item");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to delete wish item");
            }
        }
    }
}
