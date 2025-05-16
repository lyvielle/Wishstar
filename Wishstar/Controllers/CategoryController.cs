using Microsoft.AspNetCore.Mvc;
using Wishstar.Components.Pages;
using Wishstar.Extensions;
using Wishstar.Models;

namespace Wishstar.Controllers {
    [ApiController]
    [Route("/api/categories")]
    public class CategoryController(ILogger<CategoryController> logger) : ControllerBase {
        private readonly ILogger<CategoryController> _Logger = logger;

        public record class AddCategoryRequest(WishCategory WishCategory);
        public record class UpdateCategoryRequest(WishCategory WishCategory);
        public record class DeleteCategoryRequest(int CategoryId);

        [HttpPost]
        [Route("add")]
        public IActionResult Add([FromBody] AddCategoryRequest request) {
            try {
                if (!Request.TryValidateLogin(out _, out _)) {
                    return Unauthorized("You must be logged in to add a category");
                }

                if (string.IsNullOrWhiteSpace(request.WishCategory.CategoryName)) {
                    return BadRequest("Category name is required");
                }

                if (request.WishCategory.CategoryId <= 0) {
                    return BadRequest("Category id cannot be 0 or negative");
                }

                var category = WishDatabase.Load().GetCategoryById(request.WishCategory.CategoryId);
                if (category != null) {
                    return BadRequest("Category id is already ins use");
                }

                category = WishDatabase.Load().GetCategoryByName(request.WishCategory.CategoryName);
                if (category != null) {
                    return BadRequest("Category name is already in use");
                }

                WishDatabase.Load().AddCategory(request.WishCategory);

                return Ok();
            } catch (Exception ex) {
                _Logger.LogError(ex, "Failed to add category");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to add category");
            }
        }

        [HttpPost]
        [Route("update")]
        public IActionResult Update([FromBody] UpdateCategoryRequest request) {
            try {
                if (!Request.TryValidateLogin(out _, out _)) {
                    return Unauthorized("You must be logged in to update a category");
                }

                if (string.IsNullOrWhiteSpace(request.WishCategory.CategoryName)) {
                    return BadRequest("Category name is required");
                }

                var category = WishDatabase.Load().GetCategoryById(request.WishCategory.CategoryId);
                if (category == null) {
                    return BadRequest("Category does not exist");
                }

                WishDatabase.Load().UpdateCategory(request.WishCategory.CategoryId, request.WishCategory);

                return Ok();
            } catch (Exception ex) {
                _Logger.LogError(ex, "Failed to update category");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to update category");
            }
        }

        [HttpPost]
        [Route("delete")]
        public IActionResult Delete([FromBody] DeleteCategoryRequest request) {
            try {
                if (!Request.TryValidateLogin(out _, out _)) {
                    return Unauthorized("You must be logged in to delete a category");
                }

                if (request.CategoryId <= 0) {
                    return BadRequest("Category id cannot be 0 or negative");
                }

                var category = WishDatabase.Load().GetCategoryById(request.CategoryId);
                if (category == null) {
                    return BadRequest("Category does not exist");
                }

                WishDatabase.Load().DeleteCategory(category);

                int uncategorizedId = WishCategory.GetUncategorized().CategoryId;
                foreach (var wish in WishDatabase.Load().GetWishes()) {
                    if (wish.CategoryId == request.CategoryId) {
                        wish.CategoryId = uncategorizedId;
                        WishDatabase.Load().UpdateWish(wish.WishId, wish);
                    }
                }

                return Ok();
            } catch (Exception ex) {
                _Logger.LogError(ex, "Failed to delete category");
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to delete category");
            }
        }
    }
}