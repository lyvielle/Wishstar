using Microsoft.AspNetCore.Mvc;
using Wishstar.Extensions;
using Wishstar.Models;
using Wishstar.Services;

namespace Wishstar.Controllers {
    [ApiController]
    [Route("/api/vendors")]
    public class VendorController : ControllerBase {
        public record class AddVendorRequest(Vendor Vendor);
        public record class UpdateVendorRequest(Vendor Vendor);
        public record class DeleteVendorRequest(int VendorId);

        [HttpPost("add")]
        public IActionResult Add([FromBody] AddVendorRequest request) {
            if (!Request.TryValidateLogin(out _, out _)) {
                return Unauthorized("You must be logged in to add a vendor");
            }

            return ToActionResult(WishService.AddVendor(request.Vendor));
        }

        [HttpPost("update")]
        public IActionResult Update([FromBody] UpdateVendorRequest request) {
            if (!Request.TryValidateLogin(out _, out _)) {
                return Unauthorized("You must be logged in to update a vendor");
            }

            return ToActionResult(WishService.UpdateVendor(request.Vendor));
        }

        [HttpPost("delete")]
        public IActionResult Delete([FromBody] DeleteVendorRequest request) {
            if (!Request.TryValidateLogin(out _, out _)) {
                return Unauthorized("You must be logged in to delete a vendor");
            }

            return ToActionResult(WishService.DeleteVendor(request.VendorId));
        }

        private IActionResult ToActionResult(ServiceResult result) {
            return result.Success ? Ok() : BadRequest(result.Error);
        }
    }
}
