using Microsoft.AspNetCore.Mvc;
using Wishstar.Extensions;
using Wishstar.Models;
using Wishstar.Services;

namespace Wishstar.Controllers {
    [ApiController]
    [Route("/api/categories")]
    public class CategoryController : ControllerBase {
        public record class AddCategoryRequest(WishCategory WishCategory);
        public record class UpdateCategoryRequest(WishCategory WishCategory);
        public record class DeleteCategoryRequest(int CategoryId);

        [HttpPost("add")]
        public IActionResult Add([FromBody] AddCategoryRequest request) {
            if (!Request.TryValidateLogin(out _, out _)) {
                return Unauthorized("You must be logged in to add a category");
            }

            return ToActionResult(WishService.AddCategory(request.WishCategory));
        }

        [HttpPost("update")]
        public IActionResult Update([FromBody] UpdateCategoryRequest request) {
            if (!Request.TryValidateLogin(out _, out _)) {
                return Unauthorized("You must be logged in to update a category");
            }

            return ToActionResult(WishService.UpdateCategory(request.WishCategory));
        }

        [HttpPost("delete")]
        public IActionResult Delete([FromBody] DeleteCategoryRequest request) {
            if (!Request.TryValidateLogin(out _, out _)) {
                return Unauthorized("You must be logged in to delete a category");
            }

            return ToActionResult(WishService.DeleteCategory(request.CategoryId));
        }

        private IActionResult ToActionResult(ServiceResult result) {
            return result.Success ? Ok() : BadRequest(result.Error);
        }
    }
}
