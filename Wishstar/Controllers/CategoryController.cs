using Microsoft.AspNetCore.Mvc;
using Wishstar.Extensions;
using Wishstar.Models;

namespace Wishstar.Controllers {
    [ApiController]
    [Route("/api/categories")]
    public class CategoryController(ILogger<CategoryController> logger) : ControllerBase {
        private readonly ILogger<CategoryController> _Logger = logger;

        public record class AddCategoryRequest(string Name);
        public record class UpdateCategoryRequest(string OldName, string NewName);
        public record class DeleteCategoryRequest(string Name);

        [HttpPost]
        [Route("add")]
        public IActionResult Add([FromBody] AddCategoryRequest request) {
            try {
                if (!Request.TryValidateLogin(out _, out _)) {
                    return Unauthorized("You must be logged in to add a category");
                }

                if (string.IsNullOrWhiteSpace(request.Name)) {
                    return BadRequest("Category name is required");
                }

                var category = WishDatabase.Load().GetCategories().FirstOrDefault(x => x.CategoryName == request.Name);
                if (category != null) {
                    return BadRequest("Category already exists");
                }

                var newCategory = new WishCategory(request.Name);
                WishDatabase.Load().AddCategory(newCategory);

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

                if (string.IsNullOrWhiteSpace(request.OldName) || string.IsNullOrWhiteSpace(request.NewName)) {
                    return BadRequest("Both old and new category names are required");
                }

                var oldCategory = WishDatabase.Load().GetCategories().FirstOrDefault(x => x.CategoryName == request.OldName);
                if (oldCategory == null) {
                    return BadRequest("Category does not exist");
                }

                var newCategory = new WishCategory(request.NewName);
                WishDatabase.Load().UpdateCategory(oldCategory, newCategory);

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

                if (string.IsNullOrWhiteSpace(request.Name)) {
                    return BadRequest("Category name is required");
                }

                var category = WishDatabase.Load().GetCategories().FirstOrDefault(x => x.CategoryName == request.Name);
                if (category == null) {
                    return BadRequest("Category does not exist");
                }

                WishDatabase.Load().DeleteCategory(category);
                foreach (var wish in WishDatabase.Load().GetWishes()) {
                    if (wish.ItemCategory.CategoryName == request.Name) {
                        wish.ItemCategory = WishCategory.CreateUncategorized();
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