using Microsoft.AspNetCore.Mvc;
using Wishstar.Extensions;
using Wishstar.Models;
using Wishstar.Services;

namespace Wishstar.Controllers {
    [ApiController]
    [Route("/api/wishes")]
    public class WishItemController(ILogger<WishItemController> logger) : ControllerBase {
        private readonly ILogger<WishItemController> _Logger = logger;

        public record class AddWishRequest(WishItem WishItem);
        public record class UpdateWishRequest(WishItem WishItem);
        public record class DeleteWishRequest(int WishId);

        [HttpPost("add")]
        public IActionResult Add([FromBody] AddWishRequest request) {
            if (!Request.TryValidateLogin(out _, out User? user) || user == null) {
                return Unauthorized("You must be logged in to add a wish item");
            }

            if (request.WishItem is null) {
                return BadRequest("Wish item is required");
            }

            return ToActionResult(WishService.AddWish(request.WishItem, user), "add");
        }

        [HttpPost("update")]
        public IActionResult Update([FromBody] UpdateWishRequest request) {
            if (!Request.TryValidateLogin(out _, out _)) {
                return Unauthorized("You must be logged in to update a wish item");
            }

            if (request.WishItem is null) {
                return BadRequest("Wish item is required");
            }

            return ToActionResult(WishService.UpdateWish(request.WishItem), "update");
        }

        [HttpPost("delete")]
        public IActionResult Delete([FromBody] DeleteWishRequest request) {
            if (!Request.TryValidateLogin(out _, out _)) {
                return Unauthorized("You must be logged in to delete a wish item");
            }

            return ToActionResult(WishService.DeleteWish(request.WishId), "delete");
        }

        private IActionResult ToActionResult(ServiceResult result, string action) {
            if (!result.Success) {
                _Logger.LogInformation("Wish {Action} rejected: {Error}", action, result.Error);
                return BadRequest(result.Error);
            }

            return Ok();
        }
    }
}
